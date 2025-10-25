﻿using DataList.Client.Models;
using DataList.Beacon.Models;
using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading.Tasks;
using TeamServer.Beacon.Interface;
using TeamServer.Beacon.Models;
using TeamServer.Data;
using TeamServer.HandlePacket;
using TeamServer.Listener;

namespace Server.Handle_Packet
{
    internal static class Packet
    {
        
        /// <summary>
        /// 统一的Beacon会话创建方法
        /// </summary>
        public static IBeaconSession? CreateBeaconSession(IListener listener, object connectionInfo)
        {
            try
            {
                IBeaconSession? session = null;
                
                // 根据监听器类型创建相应的会话
                switch (listener.Protocol)
                {
                    case "TCP":
                        if (connectionInfo is TcpClient tcpClient)
                        {
                            session = CreateTcpBeaconSession(tcpClient, (TcpListenerSS)listener);
                        }
                        break;
                    case "ICMP":
                        if (connectionInfo is string remoteAddress)
                        {
                            session = CreateIcmpBeaconSession(remoteAddress, (ICMPListener)listener);
                        }
                        break;
                    case "UDP":
                        if (connectionInfo is IPEndPoint remoteEndPoint)
                        {
                            session = CreateUdpBeaconSession(remoteEndPoint, (UdpListenerSS)listener);
                        }
                        break;
                    case "HTTP":
                        if (connectionInfo is string clientIP)
                        {
                            session = CreateHttpBeaconSession(clientIP, (TeamServer.Listener.HttpListener)listener);
                        }
                        break;
                    case "WebSocket":
                        if (connectionInfo is WebSocket ws)
                        {
                            session = CreateWebSocketBeaconSession(ws, (WebSocketListenerSS)listener);
                        }
                        break;
                    case "SMB":
                        if (connectionInfo is (TcpClient smbTcp, IPEndPoint smbRemote))
                        {
                            session = CreateSMBBeaconSession(smbTcp, smbRemote, (SMBListener)listener);
                        }
                        break;
                    case "DHCP":
                        if (connectionInfo is IPEndPoint dhcpRemote)
                        {
                            session = CreateDHCPBeaconSession(dhcpRemote, (DHCPListener)listener);
                        }
                        break;
                    default:
                        Console.WriteLine($"[!] 不支持的监听器类型: {listener.Protocol}");
                        return null;
                }
                
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 创建Beacon会话失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建TCP Beacon会话
        /// </summary>
        public static IBeaconSession? CreateTcpBeaconSession(TcpClient tcpClient, TcpListenerSS listener)
        {
            try
            {
                var session = CreateTcpBeaconSessionInternal(tcpClient, listener);
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 创建TCP Beacon会话失败: {ex.Message}");
                return null;
            }
        }

        #region 各监听器的Beacon会话创建方法

        /// <summary>
        /// 创建ICMP Beacon会话
        /// </summary>
        private static IBeaconSession? CreateIcmpBeaconSession(string remoteAddress, ICMPListener listener)
        {
            try
            {
                var session = CreateIcmpBeaconSessionInternal(remoteAddress, listener);
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 创建ICMP Beacon会话失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建UDP Beacon会话
        /// </summary>
        private static IBeaconSession? CreateUdpBeaconSession(IPEndPoint remoteEndPoint, UdpListenerSS listener)
        {
            try
            {
                var session = CreateUdpBeaconSessionInternal(remoteEndPoint, listener);
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 创建UDP Beacon会话失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建HTTP Beacon会话
        /// </summary>
        private static IBeaconSession? CreateHttpBeaconSession(string clientIP, TeamServer.Listener.HttpListener listener)
        {
            try
            {
                var session = CreateHttpBeaconSessionInternal(clientIP, listener);
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 创建HTTP Beacon会话失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 创建WebSocket Beacon会话
        /// </summary>
        private static IBeaconSession? CreateWebSocketBeaconSession(WebSocket ws, WebSocketListenerSS listener)
        {
            try
            {
                var session = CreateWebSocketBeaconSessionInternal(ws, listener);
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 创建WebSocket Beacon会话失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建SMB Beacon会话
        /// </summary>
        private static IBeaconSession? CreateSMBBeaconSession(TcpClient tcpClient, IPEndPoint remoteEndPoint, SMBListener listener)
        {
            try
            {
                var session = CreateSMBBeaconSessionInternal(tcpClient, remoteEndPoint, listener);
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 创建SMB Beacon会话失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 创建DHCP Beacon会话
        /// </summary>
        private static IBeaconSession? CreateDHCPBeaconSession(IPEndPoint remoteEndPoint, DHCPListener listener)
        {
            try
            {
                var session = CreateDHCPBeaconSessionInternal(remoteEndPoint, listener);
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 创建DHCP Beacon会话失败: {ex.Message}");
                return null;
            }
        }

        #endregion

        /// <summary>
        /// 创建Client会话（供ClientListener使用）
        /// </summary>
        public static ClientSession? CreateClientSession(TcpClient tcpClient, ClientListener listener)
        {
            try
            {
                var session = CreateClientSessionInternal(tcpClient, listener);
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 创建Client会话失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建TCP Beacon会话（内部方法）
        /// </summary>
        private static BeaconTCPSession CreateTcpBeaconSessionInternal(TcpClient tcpClient, TcpListenerSS listener)
        {
            // 获取远程端点信息
            var remoteEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
            string ipAddress = remoteEndPoint?.Address.ToString() ?? "Unknown";
            string ipPort = remoteEndPoint != null ? $"{remoteEndPoint.Address}:{remoteEndPoint.Port}" : "Unknown";
            
            var session = new BeaconTCPSession(tcpClient)
            {
                Info = new BeaconInfo
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ListenerId = listener.Protocol,
                    IPProt = ipPort,
                    IPAddress = ipAddress,
                    ConnectedTime = DateTime.UtcNow,
                    LastSeenTime = DateTime.UtcNow,
                }
            };
            
            // 设置断开事件处理（从列表中移除并通知客户端）
            session.OnClosed += (s) =>
            {
                BeaconList.Beacons.RemoveAll(b => b.Info.Id == s.Info.Id);
                Console.WriteLine($"[-] Beacon已从列表中移除: {s.Info.IPProt}");

                // 发送断开连接通知给所有客户端
                BeaconPacketHandle.SendBeaconDisconnectedNotification(s);
            };
            
            // 设置数据接收事件
            session.OnDataReceived += async (s, data) =>
            {
                session.Info.LastSeenTime = DateTime.UtcNow;
                try
                {
                    await BeaconPacketHandle.Read(data, session);

                    // 转发监听器的连接/断开事件
                    BeaconPacketHandle.OnBeaconConnected += (beacon) => listener.TriggerBeaconConnected(beacon);
                    BeaconPacketHandle.OnBeaconDisconnected += (beacon) => listener.TriggerBeaconDisconnected(beacon);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Beacon数据包处理异常: {ex.Message}");
                }
            };
            
            return session;
        }

        #region 各监听器的Beacon会话内部实现方法

        /// <summary>
        /// 创建ICMP Beacon会话（内部方法）
        /// </summary>
        private static BeaconICMPSession CreateIcmpBeaconSessionInternal(string remoteAddress, ICMPListener listener)
        {
            var info = new BeaconInfo
            {
                Id = Guid.NewGuid().ToString("N"),
                ListenerId = listener.Protocol,
                IPProt = remoteAddress,
                IPAddress = remoteAddress,
                ConnectedTime = DateTime.UtcNow,
                LastSeenTime = DateTime.UtcNow,
            };

            var session = new BeaconICMPSession(
                async (data) => { /* ICMP发送逻辑 */ },
                () => { /* ICMP关闭逻辑 */ },
                info
            );
            
            // 设置断开事件处理
            session.OnClosed += (s) =>
            {
                BeaconList.Beacons.RemoveAll(b => b.Info.Id == s.Info.Id);
                Console.WriteLine($"[-] ICMP Beacon已从列表中移除: {s.Info.IPProt}");
                BeaconPacketHandle.SendBeaconDisconnectedNotification(s);
            };
            
            // 设置数据接收事件
            session.OnDataReceived += async (s, data) =>
            {
                session.Info.LastSeenTime = DateTime.UtcNow;
                try
                {
                    await BeaconPacketHandle.Read(data, session);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] ICMP Beacon数据包处理异常: {ex.Message}");
                }
            };
            
            return session;
        }

        /// <summary>
        /// 创建UDP Beacon会话（内部方法）
        /// </summary>
        private static BeaconUDPSession CreateUdpBeaconSessionInternal(IPEndPoint remoteEndPoint, UdpListenerSS listener)
        {
            var info = new BeaconInfo
            {
                Id = Guid.NewGuid().ToString("N"),
                ListenerId = listener.Protocol,
                IPProt = $"{remoteEndPoint.Address}:{remoteEndPoint.Port}",
                IPAddress = remoteEndPoint.Address.ToString(),
                ConnectedTime = DateTime.UtcNow,
                LastSeenTime = DateTime.UtcNow,
            };

            var session = new BeaconUDPSession(
                async (data) => { /* UDP发送逻辑 */ },
                () => { /* UDP关闭逻辑 */ },
                info
            );
            
            // 设置断开事件处理
            session.OnClosed += (s) =>
            {
                BeaconList.Beacons.RemoveAll(b => b.Info.Id == s.Info.Id);
                Console.WriteLine($"[-] UDP Beacon已从列表中移除: {s.Info.IPProt}");
                BeaconPacketHandle.SendBeaconDisconnectedNotification(s);
            };
            
            // 设置数据接收事件
            session.OnDataReceived += async (s, data) =>
            {
                session.Info.LastSeenTime = DateTime.UtcNow;
                try
                {
                    await BeaconPacketHandle.Read(data, session);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] UDP Beacon数据包处理异常: {ex.Message}");
                }
            };
            
            return session;
        }

        /// <summary>
        /// 创建HTTP Beacon会话（内部方法）
        /// </summary>
        private static BeaconHttpSession CreateHttpBeaconSessionInternal(string clientIP, TeamServer.Listener.HttpListener listener)
        {
            var info = new BeaconInfo
            {
                Id = Guid.NewGuid().ToString("N"),
                ListenerId = listener.Protocol,
                IPProt = clientIP,
                IPAddress = clientIP,
                ConnectedTime = DateTime.UtcNow,
                LastSeenTime = DateTime.UtcNow,
            };

            var session = new BeaconHttpSession(
                async (data) => { /* HTTP发送逻辑 */ },
                () => { /* HTTP关闭逻辑 */ },
                info
            );
            
            // 设置断开事件处理
            session.OnClosed += (s) =>
            {
                BeaconList.Beacons.RemoveAll(b => b.Info.Id == s.Info.Id);
                Console.WriteLine($"[-] HTTP Beacon已从列表中移除: {s.Info.IPProt}");
                BeaconPacketHandle.SendBeaconDisconnectedNotification(s);
            };
            
            // 设置数据接收事件
            session.OnDataReceived += async (s, data) =>
            {
                session.Info.LastSeenTime = DateTime.UtcNow;
                try
                {
                    await BeaconPacketHandle.Read(data, session);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] HTTP Beacon数据包处理异常: {ex.Message}");
                }
            };
            
            return session;
        }

        /// <summary>
        /// 创建WebSocket Beacon会话（内部方法）
        /// </summary>
        private static BeaconWebSocketSession CreateWebSocketBeaconSessionInternal(WebSocket ws, WebSocketListenerSS listener)
        {
            var session = new BeaconWebSocketSession(ws)
            {
                Info = new BeaconInfo
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ListenerId = listener.Protocol,
                    IPProt = "WebSocket",
                    IPAddress = "WebSocket",
                    ConnectedTime = DateTime.UtcNow,
                    LastSeenTime = DateTime.UtcNow,
                }
            };
            
            // 设置断开事件处理
            session.OnClosed += (s) =>
            {
                BeaconList.Beacons.RemoveAll(b => b.Info.Id == s.Info.Id);
                Console.WriteLine($"[-] WebSocket Beacon已从列表中移除: {s.Info.IPProt}");
                BeaconPacketHandle.SendBeaconDisconnectedNotification(s);
            };
            
            // 设置数据接收事件
            session.OnDataReceived += async (s, data) =>
            {
                session.Info.LastSeenTime = DateTime.UtcNow;
                try
                {
                    await BeaconPacketHandle.Read(data, session);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] WebSocket Beacon数据包处理异常: {ex.Message}");
                }
            };
            
            return session;
        }

        /// <summary>
        /// 创建SMB Beacon会话（内部方法）
        /// </summary>
        private static BeaconSMBSession CreateSMBBeaconSessionInternal(TcpClient tcpClient, IPEndPoint remoteEndPoint, SMBListener listener)
        {
            var session = new BeaconSMBSession(
                remoteEndPoint,
                tcpClient,
                (data) => { /* SMB发送逻辑 */ },
                () => { /* SMB关闭逻辑 */ }
            );
            
            // 设置断开事件处理
            session.OnClosed += (s) =>
            {
                BeaconList.Beacons.RemoveAll(b => b.Info.Id == s.Info.Id);
                Console.WriteLine($"[-] SMB Beacon已从列表中移除: {s.Info.IPProt}");
                BeaconPacketHandle.SendBeaconDisconnectedNotification(s);
            };
            
            // 设置数据接收事件
            session.OnDataReceived += async (s, data) =>
            {
                session.Info.LastSeenTime = DateTime.UtcNow;
                try
                {
                    await BeaconPacketHandle.Read(data, session);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] SMB Beacon数据包处理异常: {ex.Message}");
                }
            };
            
            return session;
        }

        /// <summary>
        /// 创建DHCP Beacon会话（内部方法）
        /// </summary>
        private static BeaconDHCPSession CreateDHCPBeaconSessionInternal(IPEndPoint remoteEndPoint, DHCPListener listener)
        {
            var session = new BeaconDHCPSession(
                remoteEndPoint,
                (data) => { /* DHCP发送逻辑 */ },
                () => { /* DHCP关闭逻辑 */ }
            );
            
            // 设置断开事件处理
            session.OnClosed += (s) =>
            {
                BeaconList.Beacons.RemoveAll(b => b.Info.Id == s.Info.Id);
                Console.WriteLine($"[-] DHCP Beacon已从列表中移除: {s.Info.IPProt}");
                BeaconPacketHandle.SendBeaconDisconnectedNotification(s);
            };
            
            // 设置数据接收事件
            session.OnDataReceived += async (s, data) =>
            {
                session.Info.LastSeenTime = DateTime.UtcNow;
                try
                {
                    await BeaconPacketHandle.Read(data, session);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] DHCP Beacon数据包处理异常: {ex.Message}");
                }
            };
            
            return session;
        }

        #endregion
        
        /// <summary>
        /// 创建Client会话（内部方法）
        /// </summary>
        private static ClientSession CreateClientSessionInternal(TcpClient tcpClient, IListener listener)
        {
            // 获取远程端点信息
            var remoteEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
            string ipAddress = remoteEndPoint?.Address.ToString() ?? "Unknown";
            string ipPort = remoteEndPoint != null ? $"{remoteEndPoint.Address}:{remoteEndPoint.Port}" : "Unknown";
            
            var session = new ClientSession(tcpClient)
            {
                Info = new ClientInfo
                {
                    Id = Guid.NewGuid().ToString("N"),
                    IPProt = ipPort,
                    IPAddress = ipAddress,
                    ConnectedTime = DateTime.UtcNow,
                    LastSeenTime = DateTime.UtcNow,
                }
            };
            
            // 设置数据接收事件
            session.OnDataReceived += async (s, data) =>
            {
                if (session.Info != null)
                {
                    session.Info.LastSeenTime = DateTime.UtcNow;
                }
                try
                {
                    await CientPacketHandle.Read(data, session);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Client数据包处理异常: {ex.Message}");
                }
            };
            
            return session;
        }
    }
}