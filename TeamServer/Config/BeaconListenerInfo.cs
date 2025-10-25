namespace TeamServer.Config
{
    /// <summary>
    /// Beacon监听器信息
    /// </summary>
    public class BeaconListenerInfo
    {
        /// <summary>
        /// 端口号
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 监听器类型
        /// </summary>
        public string Type { get; set; } = "";

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// 绑定地址
        /// </summary>
        public string BindAddress { get; set; } = "0.0.0.0";

        /// <summary>
        /// 构造函数
        /// </summary>
        public BeaconListenerInfo()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="type">类型</param>
        /// <param name="enabled">是否启用</param>
        /// <param name="bindAddress">绑定地址</param>
        public BeaconListenerInfo(int port, string type, bool enabled = false, string bindAddress = "0.0.0.0")
        {
            Port = port;
            Type = type;
            Enabled = enabled;
            BindAddress = bindAddress;
        }

        /// <summary>
        /// 获取显示字符串
        /// </summary>
        /// <returns>显示字符串</returns>
        public string GetDisplayString()
        {
            return $"{Port} ({Type}) - {(Enabled ? "启用" : "禁用")}";
        }

        /// <summary>
        /// 从配置字符串解析
        /// 格式：端口=类型:启用状态
        /// </summary>
        /// <param name="configString">配置字符串</param>
        /// <returns>解析结果</returns>
        public static BeaconListenerInfo Parse(string configString)
        {
            var parts = configString.Split('=');
            if (parts.Length != 2)
                return null;

            if (!int.TryParse(parts[0], out int port))
                return null;

            var valueParts = parts[1].Split(':');
            if (valueParts.Length != 2)
                return null;

            var type = valueParts[0];
            if (!bool.TryParse(valueParts[1], out bool enabled))
                return null;

            return new BeaconListenerInfo(port, type, enabled);
        }

        /// <summary>
        /// 转换为配置字符串
        /// </summary>
        /// <returns>配置字符串</returns>
        public string ToConfigString()
        {
            return $"{Port}={Type}:{Enabled.ToString().ToLower()}";
        }
    }
}

