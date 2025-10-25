using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TeamServer.Services;

namespace TeamServer.Config
{
    /// <summary>
    /// 应用程序配置管理类
    /// </summary>
    public class AppConfig
    {
        private const string ConfigFileName = "appconfig.ini";
        private static AppConfig? _instance;
        private static readonly object _lock = new object();
        private IniConfig _iniConfig;

        /// <summary>
        /// 单例实例
        /// </summary>
        public static AppConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AppConfig();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Client监听器端口
        /// </summary>
        public int ClientPort { get; set; } = 50050;

        /// <summary>
        /// 绑定地址
        /// </summary>
        public string BindAddress { get; set; } = "0.0.0.0";

        /// <summary>
        /// 是否启用Client监听器
        /// </summary>
        public bool EnableClientListener { get; set; } = true;

        /// <summary>
        /// Beacon监听器配置字典
        /// 键：端口号，值：监听器信息
        /// </summary>
        public Dictionary<int, BeaconListenerInfo> BeaconListeners { get; set; } = new Dictionary<int, BeaconListenerInfo>();

        /// <summary>
        /// ICMP监听器是否启用
        /// </summary>
        public bool IcmpEnabled { get; set; } = false;

        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel LogLevel { get; set; } = LogLevel.Info;

        /// <summary>
        /// 是否启用控制台日志输出
        /// </summary>
        public bool EnableConsoleLog { get; set; } = true;

        /// <summary>
        /// 是否启用文件日志输出
        /// </summary>
        public bool EnableFileLog { get; set; } = true;

        /// <summary>
        /// 是否启用彩色日志输出
        /// </summary>
        public bool EnableColorLog { get; set; } = true;

        /// <summary>
        /// 日志文件路径
        /// </summary>
        public string LogFilePath { get; set; } = "logs/teamserver.log";

        /// <summary>
        /// 是否显示日志时间戳
        /// </summary>
        public bool ShowLogTimestamp { get; set; } = true;

        /// <summary>
        /// 是否显示日志模块名
        /// </summary>
        public bool ShowLogModule { get; set; } = true;

        /// <summary>
        /// 是否显示日志会话信息
        /// </summary>
        public bool ShowLogSession { get; set; } = true;

        /// <summary>
        /// 添加Beacon监听器
        /// </summary>
        public async Task AddBeaconListenerAsync(int port, string type, bool enabled = false, string bindAddress = "0.0.0.0")
        {
            var listener = new BeaconListenerInfo(port, type, enabled, bindAddress);
            BeaconListeners[port] = listener;
            await SaveConfigAsync();
            Console.WriteLine($"[+] 已添加Beacon监听器: {port} ({type}) - {(enabled ? "启用" : "禁用")}");
        }

        /// <summary>
        /// 移除Beacon监听器
        /// </summary>
        public async Task RemoveBeaconListenerAsync(int port)
        {
            if (BeaconListeners.ContainsKey(port))
            {
                var listener = BeaconListeners[port];
                BeaconListeners.Remove(port);
                await SaveConfigAsync();
                Console.WriteLine($"[+] 已移除Beacon监听器: {port} ({listener.Type})");
            }
        }

        /// <summary>
        /// 更新Beacon监听器状态
        /// </summary>
        public async Task UpdateBeaconListenerAsync(int port, bool enabled)
        {
            if (BeaconListeners.ContainsKey(port))
            {
                BeaconListeners[port].Enabled = enabled;
                await SaveConfigAsync();
                Console.WriteLine($"[+] 已更新Beacon监听器: {port} ({BeaconListeners[port].Type}) - {(enabled ? "启用" : "禁用")}");
            }
        }

        /// <summary>
        /// 设置ICMP监听器状态
        /// </summary>
        public async Task SetIcmpListenerAsync(bool enabled)
        {
            IcmpEnabled = enabled;
            await SaveConfigAsync();
            Console.WriteLine($"[+] 已设置ICMP监听器: {(enabled ? "启用" : "禁用")}");
        }


        /// <summary>
        /// 构造函数，使用INI配置
        /// </summary>
        public AppConfig()
        {
            // 初始化INI配置
            _iniConfig = new IniConfig(ConfigFileName);
            
            // 设置默认值
            ClientPort = 50050;
            BindAddress = "0.0.0.0";
            EnableClientListener = true;
            
            // 初始化Beacon监听器配置
            BeaconListeners = new Dictionary<int, BeaconListenerInfo>();
            IcmpEnabled = false;
            LogLevel = LogLevel.Info;
            EnableConsoleLog = true;
            EnableFileLog = true;
            EnableColorLog = true;
            LogFilePath = "logs/teamserver.log";
            ShowLogTimestamp = true;
            ShowLogModule = true;
            ShowLogSession = true;
            
            // 加载配置文件
            LoadConfig();
        }

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                // 从INI文件加载配置
                ClientPort = _iniConfig.GetInt("Client", "Port", 50050);
                BindAddress = _iniConfig.GetString("Client", "BindAddress", "0.0.0.0");
                EnableClientListener = _iniConfig.GetBool("Client", "Enabled", true);
                
                // 加载Beacon监听器配置
                LoadBeaconListeners();
                
                // 日志配置
                var logLevelStr = _iniConfig.GetString("Logging", "LogLevel", "Info");
                LogLevel = Enum.TryParse<LogLevel>(logLevelStr, true, out var logLevel) ? logLevel : LogLevel.Info;
                EnableConsoleLog = _iniConfig.GetBool("Logging", "EnableConsoleLog", true);
                EnableFileLog = _iniConfig.GetBool("Logging", "EnableFileLog", true);
                EnableColorLog = _iniConfig.GetBool("Logging", "EnableColorLog", true);
                LogFilePath = _iniConfig.GetString("Logging", "LogFilePath", "logs/teamserver.log");
                ShowLogTimestamp = _iniConfig.GetBool("Logging", "ShowLogTimestamp", true);
                ShowLogModule = _iniConfig.GetBool("Logging", "ShowLogModule", true);
                ShowLogSession = _iniConfig.GetBool("Logging", "ShowLogSession", true);
            }
            catch (Exception ex)
            {
                // 在日志系统初始化之前，使用Console输出
                Console.WriteLine($"[!] 加载配置文件失败: {ex.Message}");
                Console.WriteLine("[+] 使用默认配置");
            }
        }

        /// <summary>
        /// 加载Beacon监听器配置
        /// </summary>
        private void LoadBeaconListeners()
        {
            BeaconListeners.Clear();
            
            // 加载ICMP配置
            IcmpEnabled = _iniConfig.GetBool("Beacon_Listeners", "ICMP", false);
            
            // 加载所有端口配置
            var allKeys = _iniConfig.GetAllKeys("Beacon_Listeners");
            foreach (var key in allKeys)
            {
                if (key == "ICMP") continue; // ICMP单独处理
                
                var value = _iniConfig.GetString("Beacon_Listeners", key, "");
                var listenerInfo = BeaconListenerInfo.Parse($"{key}={value}");
                if (listenerInfo != null)
                {
                    BeaconListeners[listenerInfo.Port] = listenerInfo;
                }
            }
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public async Task SaveConfigAsync()
        {
            try
            {
                // 保存Client监听器配置
                _iniConfig.SetInt("Client", "Port", ClientPort);
                _iniConfig.SetString("Client", "BindAddress", BindAddress);
                _iniConfig.SetBool("Client", "Enabled", EnableClientListener);
                
                // 保存Beacon监听器配置
                SaveBeaconListeners();
                
                // 保存日志配置
                _iniConfig.SetString("Logging", "LogLevel", LogLevel.ToString());
                _iniConfig.SetBool("Logging", "EnableConsoleLog", EnableConsoleLog);
                _iniConfig.SetBool("Logging", "EnableFileLog", EnableFileLog);
                _iniConfig.SetBool("Logging", "EnableColorLog", EnableColorLog);
                _iniConfig.SetString("Logging", "LogFilePath", LogFilePath);
                _iniConfig.SetBool("Logging", "ShowLogTimestamp", ShowLogTimestamp);
                _iniConfig.SetBool("Logging", "ShowLogModule", ShowLogModule);
                _iniConfig.SetBool("Logging", "ShowLogSession", ShowLogSession);
                
                // 保存到文件
                _iniConfig.SaveToFile();
                
                // 在日志系统初始化之前，使用Console输出
                Console.WriteLine($"[+] 配置已保存到: {ConfigFileName}");
            }
            catch (Exception ex)
            {
                // 在日志系统初始化之前，使用Console输出
                Console.WriteLine($"[!] 保存配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存Beacon监听器配置
        /// </summary>
        private void SaveBeaconListeners()
        {
            // 清除旧的Beacon_Listeners节
            if (_iniConfig.HasSection("Beacon_Listeners"))
            {
                // 这里需要重新实现，因为IniConfig没有删除节的方法
                // 暂时先保存新的配置
            }
            
            // 保存ICMP配置
            _iniConfig.SetBool("Beacon_Listeners", "ICMP", IcmpEnabled);
            
            // 保存所有端口配置
            foreach (var listener in BeaconListeners.Values)
            {
                _iniConfig.SetString("Beacon_Listeners", listener.Port.ToString(), listener.ToConfigString().Split('=')[1]);
            }
        }

        /// <summary>
        /// 更新Client端口
        /// </summary>
        public async Task UpdateClientPortAsync(int port)
        {
            ClientPort = port;
            await SaveConfigAsync();
        }


        /// <summary>
        /// 显示当前配置
        /// </summary>
        public void DisplayConfig()
        {
            Console.WriteLine("=== 当前配置 ===");
            Console.WriteLine($"绑定地址: {BindAddress}");
            
            // 显示启用的监听器
            Console.WriteLine("启用的监听器:");
            if (EnableClientListener)
            {
                Console.WriteLine($"  ✓ Client监听器: {BindAddress}:{ClientPort}");
            }
            else
            {
                Console.WriteLine($"  ✗ Client监听器: 未启用");
            }
            
            // 显示Beacon监听器
            DisplayBeaconListeners();
            
            // 显示日志配置
            Console.WriteLine($"日志级别: {LogLevel.GetDisplayName()}");
            Console.WriteLine($"控制台日志: {(EnableConsoleLog ? "启用" : "禁用")}");
            Console.WriteLine($"文件日志: {(EnableFileLog ? "启用" : "禁用")} (路径: {LogFilePath})");
            Console.WriteLine($"彩色日志: {(EnableColorLog ? "启用" : "禁用")}");
            Console.WriteLine("===============");
        }

        /// <summary>
        /// 显示Beacon监听器配置
        /// </summary>
        private void DisplayBeaconListeners()
        {
            // 显示ICMP监听器
            if (IcmpEnabled)
            {
                Console.WriteLine($"  ✓ Beacon ICMP监听器: {BindAddress} (ICMP)");
            }
            else
            {
                Console.WriteLine($"  ✗ Beacon ICMP监听器: 未启用");
            }
            
            // 显示端口监听器
            if (BeaconListeners.Count > 0)
            {
                foreach (var listener in BeaconListeners.Values.OrderBy(l => l.Port))
                {
                    if (listener.Enabled)
                    {
                        Console.WriteLine($"  ✓ Beacon {listener.Type}监听器: {listener.BindAddress}:{listener.Port}");
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ Beacon {listener.Type}监听器: {listener.BindAddress}:{listener.Port} (未启用)");
                    }
                }
            }
            else
            {
                Console.WriteLine("  ✗ 无Beacon端口监听器配置");
            }
        }

        /// <summary>
        /// 应用日志配置到LoggerService
        /// </summary>
        public void ApplyLogConfig()
        {
            var logger = LoggerService.Instance;
            logger.SetLogLevel(LogLevel);
            logger.EnableConsoleOutput = EnableConsoleLog;
            logger.EnableFileOutput = EnableFileLog;
            logger.EnableColorOutput = EnableColorLog;
            logger.SetLogFilePath(LogFilePath);
            logger.ShowTimestamp = ShowLogTimestamp;
            logger.ShowModule = ShowLogModule;
            logger.ShowSession = ShowLogSession;
        }
    }
}
