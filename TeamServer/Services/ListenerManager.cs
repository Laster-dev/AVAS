using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamServer.Listener;
using TeamServer.Config;
using TeamServer.Beacon.Interface;
using DataList.Client.Models;

namespace TeamServer.Services
{
    /// <summary>
    /// 监听器管理服务
    /// </summary>
    public class ListenerManager
    {
        private static ListenerManager? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// 单例实例
        /// </summary>
        public static ListenerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ListenerManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 活跃的监听器字典
        /// </summary>
        private readonly ConcurrentDictionary<string, IListener> _activeListeners = new();

        /// <summary>
        /// 支持的所有监听器类型
        /// </summary>
        public static readonly Dictionary<string, Type> SupportedListeners = new()
        {
            { "TCP", typeof(TcpListenerSS) },
            { "UDP", typeof(UdpListenerSS) },
            { "WebSocket", typeof(WebSocketListenerSS) },
            { "ICMP", typeof(ICMPListener) },
            { "DHCP", typeof(DHCPListener) },
            { "HTTP", typeof(HttpListener) },
            { "SMB", typeof(SMBListener) },
            { "Client", typeof(ClientListener) }
        };

        private ListenerManager()
        {
            // 绑定通用事件处理器
            WireCommonHandlers = (listener) =>
            {
                listener.OnBeaconConnected += (session) =>
                {
                    LoggerService.Instance.Success($"Beacon新连接: {session.Info.IPProt}", "ListenerManager", 
                        sessionId: session.Info.Id, clientIP: session.Info.IPProt, protocol: listener.Protocol);
                };
                listener.OnBeaconDisconnected += (session) =>
                {
                    LoggerService.Instance.Info($"Beacon断开连接: {session.Info.IPProt}", "ListenerManager", 
                        sessionId: session.Info.Id, clientIP: session.Info.IPProt, protocol: listener.Protocol);
                };
                listener.OnClientConnected += (session) =>
                {
                    LoggerService.Instance.Success($"Client新连接: {session.Info?.IPProt}", "ListenerManager", 
                        sessionId: session.Info?.Id, clientIP: session.Info?.IPProt, protocol: listener.Protocol);
                };
                listener.OnClientDisconnected += (session) =>
                {
                    LoggerService.Instance.Info($"Client断开连接: {session.Info?.IPProt}", "ListenerManager", 
                        sessionId: session.Info?.Id, clientIP: session.Info?.IPProt, protocol: listener.Protocol);
                };
                listener.OnError += (ex) =>
                {
                    LoggerService.Instance.Error($"监听器错误: {ex.Message}", "ListenerManager", ex, protocol: listener.Protocol);
                };
            };
        }

        /// <summary>
        /// 通用事件绑定方法
        /// </summary>
        private Action<IListener> WireCommonHandlers { get; }

        /// <summary>
        /// 初始化监听器管理器
        /// </summary>
        public async Task InitializeAsync()
        {
            var config = AppConfig.Instance;
            
            // 启动Client监听器
            if (config.EnableClientListener)
            {
                await AddListenerAsync("Client", config.BindAddress, config.ClientPort);
            }

            // 启动各种Beacon监听器（仅当配置启用时）
            await StartBeaconListeners(config);

            // 注意：其他监听器（UDP、WebSocket、ICMP、DHCP、HTTP、SMB）不再自动启动
            // 这些监听器需要通过Client消息动态添加
        }

        /// <summary>
        /// 启动Beacon监听器
        /// </summary>
        private async Task StartBeaconListeners(AppConfig config)
        {
            // 启动ICMP监听器
            if (config.IcmpEnabled)
            {
                await AddListenerAsync("ICMP", config.BindAddress, 0);
            }

            // 启动端口监听器
            foreach (var listener in config.BeaconListeners.Values)
            {
                if (listener.Enabled)
                {
                    await AddListenerAsync(listener.Type, listener.BindAddress, listener.Port);
                }
            }
        }

        /// <summary>
        /// 添加监听器
        /// </summary>
        public async Task<bool> AddListenerAsync(string type, string bindAddress, int port)
        {
            try
            {
                var listenerId = $"{type}_{bindAddress}_{port}";
                
                if (_activeListeners.ContainsKey(listenerId))
                {
                    Console.WriteLine($"[!] 监听器已存在: {listenerId}");
                    return false;
                }

                if (!SupportedListeners.TryGetValue(type, out var listenerType))
                {
                    Console.WriteLine($"[!] 不支持的监听器类型: {type}");
                    return false;
                }

                var listener = (IListener)Activator.CreateInstance(listenerType, bindAddress, port);
                if (listener == null)
                {
                    Console.WriteLine($"[!] 无法创建监听器实例: {type}");
                    return false;
                }

                WireCommonHandlers(listener);
                await listener.StartAsync();

                _activeListeners[listenerId] = listener;
                
                // 保存配置
                await SaveListenerConfigAsync(type, bindAddress, port, true);
                
                Console.WriteLine($"[+] 监听器已添加: {type} {bindAddress}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 添加监听器失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 删除监听器
        /// </summary>
        public async Task<bool> RemoveListenerAsync(string type, string bindAddress, int port)
        {
            try
            {
                var listenerId = $"{type}_{bindAddress}_{port}";
                
                if (!_activeListeners.TryRemove(listenerId, out var listener))
                {
                    Console.WriteLine($"[!] 监听器不存在: {listenerId}");
                    return false;
                }

                await listener.StopAsync();
                
                // 保存配置
                await SaveListenerConfigAsync(type, bindAddress, port, false);
                
                Console.WriteLine($"[+] 监听器已删除: {type} {bindAddress}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 删除监听器失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止监听器
        /// </summary>
        public async Task<bool> StopListenerAsync(string type, string bindAddress, int port)
        {
            try
            {
                var listenerId = $"{type}_{bindAddress}_{port}";
                
                if (!_activeListeners.TryGetValue(listenerId, out var listener))
                {
                    Console.WriteLine($"[!] 监听器不存在: {listenerId}");
                    return false;
                }

                await listener.StopAsync();
                Console.WriteLine($"[+] 监听器已停止: {type} {bindAddress}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 停止监听器失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 启动监听器
        /// </summary>
        public async Task<bool> StartListenerAsync(string type, string bindAddress, int port)
        {
            try
            {
                var listenerId = $"{type}_{bindAddress}_{port}";
                
                if (!_activeListeners.TryGetValue(listenerId, out var listener))
                {
                    Console.WriteLine($"[!] 监听器不存在: {listenerId}");
                    return false;
                }

                if (listener.IsRunning)
                {
                    Console.WriteLine($"[!] 监听器已在运行: {listenerId}");
                    return false;
                }

                await listener.StartAsync();
                Console.WriteLine($"[+] 监听器已启动: {type} {bindAddress}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 启动监听器失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取所有活跃的监听器
        /// </summary>
        public List<ListenerInfo> GetActiveListeners()
        {
            return _activeListeners.Values.Select(l => new ListenerInfo
            {
                Type = l.Protocol,
                BindAddress = l.BindAddress,
                Port = l.Port,
                IsRunning = l.IsRunning
            }).ToList();
        }

        /// <summary>
        /// 获取支持的监听器类型
        /// </summary>
        public List<string> GetSupportedListeners()
        {
            return SupportedListeners.Keys.ToList();
        }

        /// <summary>
        /// 保存监听器配置
        /// </summary>
        private async Task SaveListenerConfigAsync(string type, string bindAddress, int port, bool enabled)
        {
            try
            {
                var config = AppConfig.Instance;
                
                switch (type.ToUpper())
                {
                    case "CLIENT":
                        if (enabled)
                        {
                            config.ClientPort = port;
                            config.EnableClientListener = true;
                        }
                        else
                        {
                            config.EnableClientListener = false;
                        }
                        break;
                    case "ICMP":
                        await config.SetIcmpListenerAsync(enabled);
                        break;
                    default:
                        // 其他类型的Beacon监听器
                        if (enabled)
                        {
                            await config.AddBeaconListenerAsync(port, type, true, bindAddress);
                        }
                        else
                        {
                            await config.UpdateBeaconListenerAsync(port, false);
                        }
                        break;
                }
                
                await config.SaveConfigAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 保存监听器配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止所有监听器
        /// </summary>
        public async Task StopAllListenersAsync()
        {
            var tasks = _activeListeners.Values.Select(l => l.StopAsync());
            await Task.WhenAll(tasks);
            _activeListeners.Clear();
        }
    }

    /// <summary>
    /// 监听器信息
    /// </summary>
    public class ListenerInfo
    {
        public string Type { get; set; } = string.Empty;
        public string BindAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool IsRunning { get; set; }
    }
}

