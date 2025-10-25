using DataList.Beacon.Models;
using System;
using System.Threading.Tasks;
using TeamServer.Beacon.Interface;

namespace TeamServer.Beacon.Models
{
	/// <summary>
	/// Beacon 的 UDP 会话封装。发送通过外部提供的委托完成，接收由监听器调用触发。
	/// </summary>
	public class BeaconUDPSession : IBeaconSession
	{
		private readonly Func<byte[], Task> _sendFunc;
		private readonly Action _closeAction;

		public BeaconUDPSession(Func<byte[], Task> sendFunc, Action closeAction, BeaconInfo info)
		{
			_sendFunc = sendFunc;
			_closeAction = closeAction;
			Info = info;
		}

		public BeaconInfo Info { get; set; }

		public event Action<IBeaconSession, byte[]>? OnDataReceived;
		public event Action<IBeaconSession>? OnClosed;

		public Task SendAsync(byte[] data)
		{
			// 为与TCP一致，下行发送添加4字节大端长度头，再经UDP可靠发送
			var framed = new byte[4 + data.Length];
			framed[0] = (byte)((data.Length >> 24) & 0xFF);
			framed[1] = (byte)((data.Length >> 16) & 0xFF);
			framed[2] = (byte)((data.Length >> 8) & 0xFF);
			framed[3] = (byte)(data.Length & 0xFF);
			Buffer.BlockCopy(data, 0, framed, 4, data.Length);
			return _sendFunc != null ? _sendFunc.Invoke(framed) : Task.CompletedTask;
		}

		public Task CloseAsync()
		{
			try { _closeAction?.Invoke(); } catch { }
			OnClosed?.Invoke(this);
			return Task.CompletedTask;
		}

		public void TriggerDataReceived(byte[] data)
		{
			OnDataReceived?.Invoke(this, data);
		}
	}
}


