using System;
using System.Net;
using System.Threading.Tasks;
using TeamServer.Beacon.Interface;
using DataList.Beacon.Models;

namespace TeamServer.Beacon.Models
{
	public class BeaconDHCPSession : IBeaconSession
	{
		private readonly IPEndPoint _clientEndpoint;
		private readonly Action<byte[]> _sendCallback;
		private readonly Action _closeCallback;

		public BeaconDHCPSession(IPEndPoint clientEndpoint, Action<byte[]> sendCallback, Action closeCallback)
		{
			_clientEndpoint = clientEndpoint;
			_sendCallback = sendCallback;
			_closeCallback = closeCallback;
		}

		public BeaconInfo Info { get; set; } = new BeaconInfo
		{
			Id = Guid.NewGuid().ToString("N"),
			ListenerId = "DHCP",
			IPProt = "0.0.0.0:0",
			IPAddress = "0.0.0.0",
			ConnectedTime = DateTime.UtcNow,
			LastSeenTime = DateTime.UtcNow,
		};

		public event Action<IBeaconSession, byte[]>? OnDataReceived;
		public event Action<IBeaconSession>? OnClosed;

		public Task SendAsync(byte[] data)
		{
			try
			{
				// 直接发送数据到 DHCP Option 43
				_sendCallback?.Invoke(data);
			}
			catch
			{
				CloseAsync();
			}
			return Task.CompletedTask;
		}

		public Task CloseAsync()
		{
			try
			{
				_closeCallback?.Invoke();
			}
			finally
			{
				OnClosed?.Invoke(this);
			}
			return Task.CompletedTask;
		}

		public void TriggerDataReceived(byte[] data)
		{
			Info.LastSeenTime = DateTime.UtcNow;
			OnDataReceived?.Invoke(this, data);
		}

	}
}
