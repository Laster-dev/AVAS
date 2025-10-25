using DataList.Beacon.Models;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TeamServer.Beacon.Interface;

namespace TeamServer.Beacon.Models
{
    /// <summary>
    /// Beacon会话实现类
    /// </summary>
    public class BeaconTCPSession : IBeaconSession
    {
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _networkStream;
        private readonly CancellationTokenSource _cts = new();
        private readonly byte[] _receiveBuffer = new byte[8192];
        // 累积缓冲区, 处理TCP粘包/分包。格式: [4字节长度(大端)] + [payload]
        private byte[] _accumulator = Array.Empty<byte>();

        public BeaconTCPSession(TcpClient tcpClient)
        {
            _tcpClient = tcpClient;
            _networkStream = tcpClient.GetStream();
            _ = Task.Run(ReceiveLoopAsync);
        }

        public BeaconInfo Info { get; set; }

        /// <summary>
        /// 收到数据事件
        /// </summary>
        public event Action<IBeaconSession, byte[]>? OnDataReceived;

        /// <summary>
        /// 连接关闭事件
        /// </summary>
        public event Action<IBeaconSession>? OnClosed;

        /// <summary>
        /// 异步发送数据到Beacon
        /// </summary>
        /// <param name="data">要发送的数据</param>
        public virtual async Task SendAsync(byte[] data)
        {
            if (_networkStream == null || !_tcpClient.Connected)
            {
                return;
            }

            try
            {
                // 发送时添加4字节长度前缀（大端序）
                Span<byte> header = stackalloc byte[4];
                int len = data.Length;
                header[0] = (byte)((len >> 24) & 0xFF);
                header[1] = (byte)((len >> 16) & 0xFF);
                header[2] = (byte)((len >> 8) & 0xFF);
                header[3] = (byte)(len & 0xFF);

                await _networkStream.WriteAsync(header.ToArray(), 0, 4, _cts.Token).ConfigureAwait(false);
                await _networkStream.WriteAsync(data, 0, data.Length, _cts.Token).ConfigureAwait(false);
                await _networkStream.FlushAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await CloseAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// 关闭会话
        /// </summary>
        public virtual async Task CloseAsync()
        {
            try
            {
                _cts.Cancel();
                try { _networkStream.Close(); } catch { }
                try { _tcpClient.Close(); } catch { }
            }
            finally
            {
                OnClosed?.Invoke(this);
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// 触发数据接收事件
        /// </summary>
        /// <param name="data">接收到的数据</param>
        protected virtual void TriggerDataReceived(byte[] data)
        {
            OnDataReceived?.Invoke(this, data);
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    int bytesRead = await _networkStream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length, _cts.Token).ConfigureAwait(false);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    AppendAndProcess(_receiveBuffer, bytesRead);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
                // connection closed
            }
            catch (Exception)
            {
                // swallow and close
            }
            finally
            {
                await CloseAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 累积并解析完整消息: [4字节长度(大端)] + [payload]
        /// 可能一次读到多条消息，也可能读到半条消息
        /// </summary>
        /// <param name="buffer">新读取的数据缓冲区</param>
        /// <param name="count">有效字节数</param>
        private void AppendAndProcess(byte[] buffer, int count)
        {
            if (count <= 0)
            {
                return;
            }

            this.Info.LastSeenTime = DateTime.UtcNow;

            // 将新数据追加到累积缓冲
            if (_accumulator.Length == 0)
            {
                _accumulator = new byte[count];
                Buffer.BlockCopy(buffer, 0, _accumulator, 0, count);
            }
            else
            {
                var combined = new byte[_accumulator.Length + count];
                Buffer.BlockCopy(_accumulator, 0, combined, 0, _accumulator.Length);
                Buffer.BlockCopy(buffer, 0, combined, _accumulator.Length, count);
                _accumulator = combined;
            }

            int offset = 0;
            while (true)
            {
                // 需要至少4字节长度头
                if (_accumulator.Length - offset < 4)
                {
                    break;
                }

                int payloadLength =
                    (_accumulator[offset] << 24) |
                    (_accumulator[offset + 1] << 16) |
                    (_accumulator[offset + 2] << 8) |
                    (_accumulator[offset + 3]);

                if (payloadLength < 0)
                {
                    // 非法长度，丢弃所有数据并关闭
                    _accumulator = Array.Empty<byte>();
                    _ = CloseAsync();
                    return;
                }

                // 检查是否已经拥有完整payload
                if (_accumulator.Length - offset - 4 < payloadLength)
                {
                    // 不完整，保留
                    break;
                }

                // 提取完整消息
                var message = new byte[payloadLength];
                Buffer.BlockCopy(_accumulator, offset + 4, message, 0, payloadLength);
                TriggerDataReceived(message);

                // 前移offset
                offset += 4 + payloadLength;

                // 循环处理可能的下一条
            }

            // 将未处理的尾部保留到新的累积缓冲
            if (offset == 0)
            {
                // 没有解析出任何完整帧，保持现状
                return;
            }

            if (offset >= _accumulator.Length)
            {
                _accumulator = Array.Empty<byte>();
            }
            else
            {
                int remaining = _accumulator.Length - offset;
                var rest = new byte[remaining];
                Buffer.BlockCopy(_accumulator, offset, rest, 0, remaining);
                _accumulator = rest;
            }
        }
    }
}