using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TeamServer.Beacon.Interface;
using DataList.Beacon.Models;

namespace TeamServer.Beacon.Models
{
	public class BeaconWebSocketSession : IBeaconSession
	{
		private readonly WebSocket _socket;
		private readonly CancellationTokenSource _cts = new CancellationTokenSource();
		private readonly byte[] _buffer = new byte[8192];

		public BeaconWebSocketSession(WebSocket socket)
		{
			_socket = socket;
			_ = Task.Run(ReceiveLoopAsync);
		}

		public BeaconInfo Info { get; set; }

		public event Action<IBeaconSession, byte[]>? OnDataReceived;
		public event Action<IBeaconSession>? OnClosed;

		public async Task SendAsync(byte[] data)
		{
			if (_socket.State != WebSocketState.Open) return;
			try
			{
				await _socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, _cts.Token).ConfigureAwait(false);
			}
			catch
			{
				await CloseAsync().ConfigureAwait(false);
			}
		}

		public async Task CloseAsync()
		{
			try
			{
				_cts.Cancel();
				try { if (_socket.State == WebSocketState.Open) await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "close", CancellationToken.None).ConfigureAwait(false); } catch { }
			}
			finally
			{
				OnClosed?.Invoke(this);
			}
		}

		private async Task ReceiveLoopAsync()
		{
			try
			{
				var acc = Array.Empty<byte>();
				while (!_cts.IsCancellationRequested && _socket.State == WebSocketState.Open)
				{
					var result = await _socket.ReceiveAsync(new ArraySegment<byte>(_buffer), _cts.Token).ConfigureAwait(false);
					if (result.MessageType == WebSocketMessageType.Close)
					{
						break;
					}
					// accumulate until EndOfMessage
					if (acc.Length == 0)
					{
						acc = new byte[result.Count];
						Buffer.BlockCopy(_buffer, 0, acc, 0, result.Count);
					}
					else
					{
						var combined = new byte[acc.Length + result.Count];
						Buffer.BlockCopy(acc, 0, combined, 0, acc.Length);
						Buffer.BlockCopy(_buffer, 0, combined, acc.Length, result.Count);
						acc = combined;
					}

					if (result.EndOfMessage)
					{
						Info.LastSeenTime = DateTime.UtcNow;
						OnDataReceived?.Invoke(this, acc);
						acc = Array.Empty<byte>();
					}
				}
			}
			catch
			{
			}
			finally
			{
				await CloseAsync().ConfigureAwait(false);
			}
		}
	}
}
