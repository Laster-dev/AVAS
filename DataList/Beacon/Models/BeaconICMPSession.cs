using System;
using System.Threading.Tasks;
using TeamServer.Beacon.Interface;
using DataList.Beacon.Models;

namespace TeamServer.Beacon.Models
{
    /// <summary>
    /// Beacon的ICMP会话实现类
    /// </summary>
    public class BeaconICMPSession : IBeaconSession
    {
        // ICMP会话使用回调函数来发送数据
        private readonly Func<byte[], Task> _sendFunc;
        private readonly Action _closeAction;
        
        public BeaconICMPSession(Func<byte[], Task> sendFunc, Action closeAction, BeaconInfo info)
        {
            _sendFunc = sendFunc;
            _closeAction = closeAction;
            Info = info;
        }

        public BeaconInfo Info { get; set; }

        /// <summary>
        /// 异步发送数据到Beacon
        /// </summary>
        /// <param name="data">要发送的数据</param>
        public async Task SendAsync(byte[] data)
        {
            try
            {
                // 为与TCP一致，下行发送添加4字节大端长度头
                var framed = new byte[4 + data.Length];
                framed[0] = (byte)((data.Length >> 24) & 0xFF);
                framed[1] = (byte)((data.Length >> 16) & 0xFF);
                framed[2] = (byte)((data.Length >> 8) & 0xFF);
                framed[3] = (byte)(data.Length & 0xFF);
                Buffer.BlockCopy(data, 0, framed, 4, data.Length);
                
                if (_sendFunc != null)
                    await _sendFunc.Invoke(framed).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] ICMP会话发送数据失败: {ex.Message}");
                await CloseAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 关闭会话
        /// </summary>
        public async Task CloseAsync()
        {
            try 
            { 
                _closeAction?.Invoke(); 
            } 
            catch { }
            
            OnClosed?.Invoke(this);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 收到数据事件
        /// </summary>
        public event Action<IBeaconSession, byte[]> OnDataReceived;

        /// <summary>
        /// 连接关闭事件
        /// </summary>
        public event Action<IBeaconSession> OnClosed;

        /// <summary>
        /// 触发数据接收事件
        /// </summary>
        /// <param name="data">接收到的数据</param>
        public void TriggerDataReceived(byte[] data)
        {
            Info.LastSeenTime = DateTime.UtcNow;
            OnDataReceived?.Invoke(this, data);
        }
    }
}