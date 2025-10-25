using DataList.Beacon.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeamServer.Beacon.Interface
{
    /// <summary>
    /// Beacon会话接口，表示一个连接的肉鸡会话
    /// </summary>
    public interface IBeaconSession
    {
        
        public BeaconInfo Info { get; set; }

        /// <summary>
        /// 异步发送数据到Beacon
        /// </summary>
        /// <param name="data">要发送的数据</param>
        Task SendAsync(byte[] data);

        /// <summary>
        /// 关闭会话
        /// </summary>
        Task CloseAsync();

        /// <summary>
        /// 收到数据事件
        /// </summary>
        event Action<IBeaconSession, byte[]> OnDataReceived;

        /// <summary>
        /// 连接关闭事件
        /// </summary>
        event Action<IBeaconSession> OnClosed;
    }
}
