using System.Collections.Generic;

namespace TeamServer.Config
{
    /// <summary>
    /// Beacon监听器配置
    /// </summary>
    public class BeaconListenerConfig
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// 端口列表
        /// </summary>
        public List<int> Ports { get; set; } = new List<int>();

        /// <summary>
        /// 绑定地址
        /// </summary>
        public string BindAddress { get; set; } = "0.0.0.0";

        /// <summary>
        /// 监听器类型名称
        /// </summary>
        public string TypeName { get; set; } = "";

        /// <summary>
        /// 构造函数
        /// </summary>
        public BeaconListenerConfig()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="typeName">类型名称</param>
        /// <param name="enabled">是否启用</param>
        /// <param name="ports">端口列表</param>
        public BeaconListenerConfig(string typeName, bool enabled = false, List<int> ports = null)
        {
            TypeName = typeName;
            Enabled = enabled;
            Ports = ports ?? new List<int>();
        }

        /// <summary>
        /// 添加端口
        /// </summary>
        /// <param name="port">端口号</param>
        public void AddPort(int port)
        {
            if (!Ports.Contains(port))
            {
                Ports.Add(port);
            }
        }

        /// <summary>
        /// 移除端口
        /// </summary>
        /// <param name="port">端口号</param>
        public void RemovePort(int port)
        {
            Ports.Remove(port);
        }

        /// <summary>
        /// 设置端口列表
        /// </summary>
        /// <param name="ports">端口列表</param>
        public void SetPorts(List<int> ports)
        {
            Ports = ports ?? new List<int>();
        }

        /// <summary>
        /// 获取端口字符串
        /// </summary>
        /// <returns>端口字符串</returns>
        public string GetPortsString()
        {
            return string.Join(", ", Ports);
        }

        /// <summary>
        /// 是否有配置的端口
        /// </summary>
        /// <returns>是否有端口</returns>
        public bool HasPorts()
        {
            return Ports.Count > 0;
        }
    }
}
