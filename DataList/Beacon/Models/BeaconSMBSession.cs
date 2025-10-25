using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using TeamServer.Beacon.Interface;
using DataList.Beacon.Models;

namespace DataList.Beacon.Models
{
    public class BeaconSMBSession : IBeaconSession
    {
        private readonly IPEndPoint _clientEndpoint;
        private readonly TcpClient _tcpClient;
        private readonly Action<byte[]> _sendCallback;
        private readonly Action _closeCallback;

        public BeaconSMBSession(IPEndPoint clientEndpoint, TcpClient tcpClient, Action<byte[]> sendCallback, Action closeCallback)
        {
            _clientEndpoint = clientEndpoint;
            _tcpClient = tcpClient;
            _sendCallback = sendCallback;
            _closeCallback = closeCallback;
        }

        public BeaconInfo Info { get; set; } = new BeaconInfo
        {
            Id = Guid.NewGuid().ToString("N"),
            ListenerId = "SMB",
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
                // 通过SMB协议发送数据
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
                _tcpClient?.Close();
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
