using DataList.Client.Models;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TeamServer.HandlePacket;
using TeamServer.Beacon.Interface;
using TeamServer.Data;

namespace TeamServer.Listener
{
    /// <summary>
    /// Client监听器实现
    /// </summary>
    internal class ClientListener : IListener
    {
        private readonly string _bindAddress;                                               // 监听地址
        private readonly int _port;                                                         // 监听端口
        private TcpListener? _tcpListener;                                                  // TcpListener实例
        private CancellationTokenSource? _cts;                                              // 用于取消异步操作
        private readonly ConcurrentDictionary<string, ClientSession> _clientSessions = new(); // 活跃的Client会话

        // 清理相关
        private Task? _cleanupTask;                                                          // 清理任务
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(30);               // 清理间隔
        private readonly TimeSpan _clientIdleTimeout = TimeSpan.FromMinutes(2);              // Client超时

        public ClientListener(string bindAddress, int port = 50050)
        {
            _bindAddress = bindAddress;
            _port = port;
        }

        public bool IsRunning { get; private set; }

        public string Protocol => "TCP-CLIENT";

        public string BindAddress => _bindAddress;

        public int Port => _port;

        /// <summary>
        /// 新Client连接事件
        /// </summary>
        public event Action<ClientSession>? OnClientConnected;

        /// <summary>
        /// Client断开连接事件
        /// </summary>
        public event Action<ClientSession>? OnClientDisconnected;

        /// <summary>
        /// 新Beacon连接事件（空实现，ClientListener只处理Client）
        /// </summary>
        public event Action<IBeaconSession>? OnBeaconConnected;

        /// <summary>
        /// Beacon断开连接事件（空实现，ClientListener只处理Client）
        /// </summary>
        public event Action<IBeaconSession>? OnBeaconDisconnected;

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
                
                // 直接处理Client连接
                _ = Task.Run(() => HandleConnectionAsync(tcpClient, remoteEndPoint));
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                try { tcpClient.Close(); } catch { }
            }
        }
        
        private void RegisterClientSession(ClientSession session, IPEndPoint? remoteEndPoint)
        {
            // 信息已经在Packet中设置，不需要重复设置
            _clientSessions[session.Info?.Id ?? Guid.NewGuid().ToString("N")] = session;
            
            // 添加到全局客户端列表
            ClientList.Clients.Add(session);

            session.OnClosed += s =>
            {
                if (s.Info != null)
                {
                    _clientSessions.TryRemove(s.Info.Id, out _);
                    // 从全局列表中移除
                    ClientList.Clients.RemoveAll(c => c.Info?.Id == s.Info.Id);
                }
                OnClientDisconnected?.Invoke(s);
            };

            OnClientConnected?.Invoke(session);
            Console.WriteLine($"[+] Client会话已注册: {session.Info?.IPProt}");
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
                        Console.WriteLine("[!] Client连接在读取长度头时关闭");
                        tcpClient.Close();
                        return;
                    }
                    lengthBytesRead += read;
                }
                
                // 解析长度（大端序）
                int payloadLength = (lengthBuffer[0] << 24) | (lengthBuffer[1] << 16) | (lengthBuffer[2] << 8) | lengthBuffer[3];
                
                if (payloadLength <= 0 || payloadLength > 1024 * 1024)
                {
                    Console.WriteLine($"[!] Client非法的payload长度: {payloadLength}");
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
                        Console.WriteLine("[!] Client连接在读取payload时关闭");
                        tcpClient.Close();
                        return;
                    }
                    payloadBytesRead += read;
                }

                // 直接创建Client会话
                var clientSession = Server.Handle_Packet.Packet.CreateClientSession(tcpClient, this);
                
                if (clientSession != null)
                {
                    RegisterClientSession(clientSession, remoteEndPoint);
                }
                else
                {
                    // 无法创建Client会话，关闭连接
                    Console.WriteLine("[!] 无法创建Client会话，关闭连接");
                    tcpClient.Close();
                }
            }
            catch (OperationCanceledException)
            {
                // 连接超时或取消，不记录为错误
                try { tcpClient.Close(); } catch { }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                Console.WriteLine($"[!] 处理Client连接时发生异常: {ex.Message}");
                try { tcpClient.Close(); } catch { }
            }
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
                    CleanupStaleClients();
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

        private void CleanupStaleClients()
        {
            var now = DateTime.UtcNow;

            ClientSession[] snapshot;
            lock (ClientList.Clients)
            {
                snapshot = ClientList.Clients.ToArray();
            }

            foreach (var client in snapshot)
            {
                var id = client.Info?.Id;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                var lastSeen = client.Info!.LastSeenTime;
                var missingInActiveMap = !_clientSessions.ContainsKey(id);
                var timedOut = (now - lastSeen) > _clientIdleTimeout;

                if (missingInActiveMap || timedOut)
                {
                    lock (ClientList.Clients)
                    {
                        ClientList.Clients.RemoveAll(c => c.Info?.Id == id);
                    }

                    _clientSessions.TryRemove(id, out _);

                    Console.WriteLine($"[-] 清理Client: {client.Info?.IPProt}, missing={missingInActiveMap}, timeout={timedOut}");
                }
            }
        }
    }
}
