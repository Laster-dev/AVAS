using DataList.Beacon.Models;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TeamServer.Beacon.Interface;
using TeamServer.Beacon.Models;
using TeamServer.HandlePacket;

namespace TeamServer.Listener
{
	internal class WebSocketListenerSS : IListener
	{
		private readonly string _bindAddress;
		private readonly int _port;
		private System.Net.HttpListener? _http;
		private CancellationTokenSource? _cts;
		private Task? _acceptTask;
		private readonly ConcurrentDictionary<string, IBeaconSession> _sessions = new();

		public WebSocketListenerSS(string bindAddress, int port)
		{
			_bindAddress = bindAddress;
			_port = port;
		}

		public bool IsRunning { get; private set; }
		public string Protocol => "WS";
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
				_http = new System.Net.HttpListener();
				// Windows 下 HttpListener 不支持 0.0.0.0，使用通配符 '+'
				var host = (_bindAddress == "0.0.0.0" || _bindAddress == "::") ? "+" : _bindAddress;
				var prefix = $"http://{host}:{_port}/";
				_http.Prefixes.Add(prefix);
				// 额外添加 localhost 以提升兼容性（部分环境需要 URLACL）
				try { if (!string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) _http.Prefixes.Add($"http://localhost:{_port}/"); } catch { }
				_http.Start();
				_cts = new CancellationTokenSource();
				IsRunning = true;
				_acceptTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
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
				try { if (_acceptTask != null) await _acceptTask.ConfigureAwait(false); } catch { }
				try { _http?.Stop(); } catch { }
			}
			catch (Exception ex)
			{
				OnError?.Invoke(ex);
				throw;
			}
		}

		private async Task AcceptLoopAsync(CancellationToken token)
		{
			if (_http == null) return;
			while (!token.IsCancellationRequested)
			{
				try
				{
					var ctx = await _http.GetContextAsync().ConfigureAwait(false);
					if (!ctx.Request.IsWebSocketRequest)
					{
						ctx.Response.StatusCode = 400; ctx.Response.Close();
						continue;
					}
					var wsCtx = await ctx.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
					var socket = wsCtx.WebSocket;
					HandleSocket(socket, ctx.Request.RemoteEndPoint as IPEndPoint);
				}
				catch (Exception ex)
				{
					OnError?.Invoke(ex);
				}
			}
		}

		private void HandleSocket(WebSocket ws, IPEndPoint? remote)
		{
			var session = new BeaconWebSocketSession(ws)
			{
				Info = new BeaconInfo
				{
					Id = Guid.NewGuid().ToString("N"),
					ListenerId = Protocol,
					IPProt = $"{remote?.Address}:{remote?.Port}",
					IPAddress = remote?.Address.ToString() ?? "Unknown",
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
				_sessions.TryRemove(s.Info.Id, out _);
				OnBeaconDisconnected?.Invoke(s);
			};

			_sessions[session.Info.Id] = session;
			OnBeaconConnected?.Invoke(session);
		}
	}
}
