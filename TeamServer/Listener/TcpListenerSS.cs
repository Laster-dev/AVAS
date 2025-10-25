﻿using DataList.Beacon.Models;
using DataList.Client.Models;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TeamServer.Beacon.Interface;
using TeamServer.Beacon.Models;
using TeamServer.Data;
using TeamServer.HandlePacket;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TeamServer.Listener
{
    /// <summary>
    /// TCP监听器实现
    /// </summary>
    internal class TcpListenerSS : IListener
    {
        private readonly string _bindAddress;                                               // 监听地址
        private readonly int _port;                                                         // 监听端口
        private TcpListener? _tcpListener;                                                  // TcpListener实例
        private CancellationTokenSource? _cts;                                              // 用于取消异步操作
        private readonly ConcurrentDictionary<string, IBeaconSession> _sessions = new();    // 活跃的Beacon会话

        // 清理相关
        private Task? _cleanupTask;                                                          // 清理任务
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(30);               // 清理间隔
        private readonly TimeSpan _beaconIdleTimeout = TimeSpan.FromSeconds(30);              // Beacon超时

        public TcpListenerSS(string bindAddress, int port)
        {
            _bindAddress = bindAddress;
            _port = port;
        }

        public bool IsRunning { get; private set; }

        public string Protocol => "TCP";

        public string BindAddress => _bindAddress;

        public int Port => _port;

        /// <summary>
        /// 新Beacon连接事件
        /// </summary>
        public event Action<IBeaconSession>? OnBeaconConnected;

        /// <summary>
        /// Beacon断开连接事件
        /// </summary>
        public event Action<IBeaconSession>? OnBeaconDisconnected;


        /// <summary>
        /// 新Client连接事件（空实现，TcpListenerSS只处理Beacon）
        /// </summary>
        public event Action<ClientSession>? OnClientConnected;

        /// <summary>
        /// Client断开连接事件（空实现，TcpListenerSS只处理Beacon）
        /// </summary>
        public event Action<ClientSession>? OnClientDisconnected;

        /// <summary>
        /// 错误事件
        /// </summary>
        public event Action<Exception>? OnError;

        public async Task StartAsync()
        {
            if (IsRunning)
            {
                return;
            }

            try
            {
                IPAddress ipAddress = IPAddress.Parse(_bindAddress);
                _tcpListener = new TcpListener(ipAddress, _port);
                _tcpListener.Start();

                _cts = new CancellationTokenSource();
                IsRunning = true;

                _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
                _cleanupTask = Task.Run(() => CleanupLoopAsync(_cts.Token));
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!IsRunning)
            {
                return;
            }

            try
            {
                IsRunning = false;
                _cts?.Cancel();
                _tcpListener?.Stop();
                try { if (_cleanupTask != null) await _cleanupTask.ConfigureAwait(false); } catch { }
                GC.Collect();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                throw;
            }
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            if (_tcpListener == null)
            {
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    HandleClient(tcpClient);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                }
            }
        }

        private void HandleClient(TcpClient tcpClient)
        {
            try
            {
                var remoteEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                
                // 不直接创建会话，让Packet来判断和分发
                _ = Task.Run(() => HandleConnectionAsync(tcpClient, remoteEndPoint));
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                try { tcpClient.Close(); } catch { }
            }
        }
        
        private void RegisterBeaconSession(IBeaconSession session, IPEndPoint? remoteEndPoint)
        {
            var beaconSession = session as BeaconTCPSession;
            if (beaconSession != null)
            {
                // 信息已经在Packet中设置，不需要重复设置
                _sessions[beaconSession.Info.Id] = beaconSession;

                // 将Beacon添加到全局列表
                BeaconList.Beacons.Add(beaconSession);
                
                beaconSession.OnClosed += s =>
                {
                    _sessions.TryRemove(beaconSession.Info.Id, out _);
                    OnBeaconDisconnected?.Invoke(s);
                };

                OnBeaconConnected?.Invoke(beaconSession);
                Console.WriteLine($"[+] Beacon会话已注册: {beaconSession.Info.IPProt}");
            }
        }
        
        
        private async Task HandleConnectionAsync(TcpClient tcpClient, IPEndPoint? remoteEndPoint)
        {
            try
            {
                var stream = tcpClient.GetStream();
                
                // 等待第一个数据包
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                
                // 先读取长度头（4字节）
                var lengthBuffer = new byte[4];
                int lengthBytesRead = 0;
                while (lengthBytesRead < 4)
                {
                    int read = await stream.ReadAsync(lengthBuffer, lengthBytesRead, 4 - lengthBytesRead, cts.Token);
                    if (read == 0) 
                    {
                        Console.WriteLine("[!] 连接在读取长度头时关闭");
                        tcpClient.Close();
                        return;
                    }
                    lengthBytesRead += read;
                }
                
                // 解析长度（大端序）
                int payloadLength = (lengthBuffer[0] << 24) | (lengthBuffer[1] << 16) | (lengthBuffer[2] << 8) | lengthBuffer[3];

                
                if (payloadLength <= 0 || payloadLength > 1024 * 1024)
                {
                    Console.WriteLine($"[!] 非法的payload长度: {payloadLength}");
                    tcpClient.Close();
                    return;
                }
                
                // 读取实际的MessagePack数据
                var payloadBuffer = new byte[payloadLength];
                int payloadBytesRead = 0;
                while (payloadBytesRead < payloadLength)
                {
                    int read = await stream.ReadAsync(payloadBuffer, payloadBytesRead, payloadLength - payloadBytesRead, cts.Token);
                    if (read == 0)
                    {
                        Console.WriteLine("[!] 连接在读取payload时关闭");
                        tcpClient.Close();
                        return;
                    }
                    payloadBytesRead += read;
                }

                //Console.WriteLine($"[DEBUG] 成功读取完整的MessagePack数据: {payloadLength} 字节");

                // 直接创建Beacon会话
                var beaconSession = Server.Handle_Packet.Packet.CreateTcpBeaconSession(tcpClient, this);
                
                if (beaconSession != null)
                {
                    RegisterBeaconSession(beaconSession, remoteEndPoint);
                    
                    // 注册完成后，处理初始数据包
                    try
                    {
                        await BeaconPacketHandle.Read(payloadBuffer, beaconSession);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[!] 处理初始数据包失败: {ex.Message}");
                    }
                }
                else
                {
                    // 无法创建Beacon会话，关闭连接
                    Console.WriteLine("[!] 无法创建Beacon会话，关闭连接");
                    tcpClient.Close();
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                Console.WriteLine($"[!] 处理连接时发生异常: {ex.Message}");
                try { tcpClient.Close(); } catch { }
            }
        }
        
        /// <summary>
        /// 公共方法，供包处理器调用以触发Beacon连接事件
        /// </summary>
        public void TriggerBeaconConnected(IBeaconSession session)
        {
            OnBeaconConnected?.Invoke(session);
        }
        
        /// <summary>
        /// 公共方法，供包处理器调用以触发Beacon断开事件
        /// </summary>
        public void TriggerBeaconDisconnected(IBeaconSession session)
        {
            OnBeaconDisconnected?.Invoke(session);
        }

        /// <summary>
        /// 周期性清理僵尸会话/超时会话
        /// </summary>
        private async Task CleanupLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    CleanupStaleBeacons();
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                }

                try
                {
                    await Task.Delay(_cleanupInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void CleanupStaleBeacons()
        {
            var now = DateTime.UtcNow;

            // 复制列表以避免枚举期间修改异常
            IBeaconSession[] snapshot;
            lock (BeaconList.Beacons)
            {
                snapshot = BeaconList.Beacons.ToArray();
            }

            foreach (var beacon in snapshot)
            {
                // 仅清理当前监听器负责的协议类型（避免误清理UDP等其他协议的会话）
                if (!string.Equals(beacon.Info.ListenerId, this.Protocol, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var id = beacon.Info.Id;
                var lastSeen = beacon.Info.LastSeenTime;

                var missingInActiveMap = !_sessions.ContainsKey(id);
                var timedOut = (now - lastSeen) > _beaconIdleTimeout;

                if (missingInActiveMap || timedOut)
                {
                    // 从全局列表移除
                    lock (BeaconList.Beacons)
                    {
                        BeaconList.Beacons.RemoveAll(b => b.Info.Id == id);
                    }

                    // 触发断开通知
                    try
                    {
                        BeaconPacketHandle.SendBeaconDisconnectedNotification(beacon);
                    }
                    catch { }

                    // 同步移除会话字典
                    _sessions.TryRemove(id, out _);

                    Console.WriteLine($"[-] 清理Beacon: {beacon.Info.IPProt}, missing={missingInActiveMap}, timeout={timedOut}");
                }
            }
        }

    }
}