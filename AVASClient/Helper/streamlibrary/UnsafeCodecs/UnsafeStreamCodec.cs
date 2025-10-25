using StreamLibrary.src;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace StreamLibrary.UnsafeCodecs
{
    public class UnsafeStreamCodec : IUnsafeCodec
    {
        public unsafe static int MemCmp(byte* ptr1, byte* ptr2, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int diff = ptr1[i] - ptr2[i];
                if (diff != 0)
                    return diff;
            }
            return 0;
        }
        /// <summary>
        /// 纯C#实现的高速memcpy，适用于非托管内存区域。
        /// </summary>
        /// <param name="dst">目标指针</param>
        /// <param name="src">源指针</param>
        /// <param name="count">拷贝字节数</param>
        public unsafe static void MemCpy(byte* dst, byte* src, int count)
        {
            Buffer.MemoryCopy(src, dst, count, count);
        }
        public unsafe static void MemCpy(IntPtr dst, IntPtr src, int count)
        {
            Buffer.MemoryCopy(src.ToPointer(), dst.ToPointer(), count, count);
        }
        public override ulong CachedSize
        {
            get;
            internal set;
        }

        public override int BufferCount
        {
            get { return 1; }
        }

        public override CodecOption CodecOptions
        {
            get { return CodecOption.RequireSameSize; }
        }

        public Size CheckBlock { get; private set; }
        private byte[] EncodeBuffer;
        private Bitmap decodedBitmap;
        private PixelFormat EncodedFormat;
        private int EncodedWidth;
        private int EncodedHeight;
        public override event IVideoCodec.VideoDebugScanningDelegate onCodeDebugScan;
        public override event IVideoCodec.VideoDebugScanningDelegate onDecodeDebugScan;

        bool UseJPEG;

        /// <summary>
        /// Initialize a new object of UnsafeStreamCodec
        /// </summary>
        /// <param name="ImageQuality">The quality to use between 0-100</param>
        public UnsafeStreamCodec(int ImageQuality = 100, bool UseJPEG = true)
            : base(ImageQuality)
        {
            this.CheckBlock = new Size(50, 1);
            this.UseJPEG = UseJPEG;
        }

        public override unsafe void CodeImage(IntPtr Scan0, Rectangle ScanArea, Size ImageSize, PixelFormat Format, Stream outStream)
        {
            lock (ImageProcessLock)
            {
                if (!outStream.CanWrite)
                    throw new InvalidOperationException("Stream 不可写");

                int pixelSize = Format switch
                {
                    PixelFormat.Format24bppRgb => 3,
                    PixelFormat.Format32bppRgb => 4, // 实际是4字节
                    PixelFormat.Format32bppArgb => 4,
                    PixelFormat.Format32bppPArgb => 4,
                    _ => throw new NotSupportedException($"不支持的像素格式: {Format}")
                };
                int stride = ImageSize.Width * pixelSize;
                int rawLength = stride * ImageSize.Height;
                byte* pScan0 = (byte*)Scan0.ToPointer();

                // 首帧：全图压缩并写入
                if (EncodeBuffer == null)
                {
                    this.EncodedFormat = Format;
                    this.EncodedWidth = ImageSize.Width;
                    this.EncodedHeight = ImageSize.Height;
                    this.EncodeBuffer = new byte[rawLength];

                    using (var tmpBmp = new Bitmap(ImageSize.Width, ImageSize.Height, Format))
                    {
                        var data = tmpBmp.LockBits(new Rectangle(0, 0, tmpBmp.Width, tmpBmp.Height), ImageLockMode.WriteOnly, tmpBmp.PixelFormat);
                        try
                        {
                            int copyWidthBytes = ImageSize.Width * pixelSize;
                            for (int row = 0; row < ImageSize.Height; row++)
                            {
                                byte* srcRow = pScan0 + (row * stride);
                                byte* dstRow = (byte*)data.Scan0.ToPointer() + (row * data.Stride);
                                MemCpy(dstRow, srcRow, copyWidthBytes);
                            }
                        }
                        finally { tmpBmp.UnlockBits(data); }

                        byte[] compressed = base.jpgCompression.Compress(tmpBmp);
                        outStream.Write(BitConverter.GetBytes(compressed.Length), 0, 4);
                        outStream.Write(compressed, 0, compressed.Length);

                        fixed (byte* ptr = EncodeBuffer)
                        {
                            MemCpy(new IntPtr(ptr), Scan0, rawLength);
                        }
                    }
                    return;
                }

                // 检查一致性
                if (this.EncodedFormat != Format)
                    throw new InvalidOperationException("像素格式与首帧不一致");
                if (this.EncodedWidth != ImageSize.Width || this.EncodedHeight != ImageSize.Height)
                    throw new InvalidOperationException("尺寸与首帧不一致");

                // 暂存需要发送的数据块
                List<Rectangle> changedBlocks = new List<Rectangle>();
                List<Rectangle> finalUpdates = new List<Rectangle>();

                // 块划分与检测变化
                int blockHeight = CheckBlock.Height;
                int blockWidth = CheckBlock.Width;
                int lastBlockY = ScanArea.Y + ((ScanArea.Height / blockHeight) * blockHeight);
                int lastBlockX = ScanArea.X + ((ScanArea.Width / blockWidth) * blockWidth);

                fixed (byte* encBuffer = EncodeBuffer)
                {
                    // 纵向大块检测
                    for (int y = ScanArea.Y; y < ScanArea.Y + ScanArea.Height;)
                    {
                        int currHeight = Math.Min(blockHeight, ScanArea.Y + ScanArea.Height - y);
                        Rectangle blockRect = new Rectangle(ScanArea.X, y, ScanArea.Width, currHeight);

                        int offset = (y * stride) + (ScanArea.X * pixelSize);
                        bool hasChange = false;
                        for (int row = 0; row < currHeight; row++)
                        {
                            if (NativeMethods.memcmp(encBuffer + offset + row * stride, pScan0 + offset + row * stride, (uint)(ScanArea.Width * pixelSize)) != 0)
                            {
                                hasChange = true;
                                break;
                            }
                        }

                        if (hasChange)
                        {
                            if (changedBlocks.Count > 0 && changedBlocks.Last().Bottom == blockRect.Top)
                            {
                                var last = changedBlocks.Last();
                                changedBlocks[changedBlocks.Count - 1] = Rectangle.Union(last, blockRect);
                            }
                            else
                            {
                                changedBlocks.Add(blockRect);
                            }
                        }
                        y += currHeight;
                    }

                    // 横向小块检测与合并
                    foreach (var block in changedBlocks)
                    {
                        for (int x = block.X; x < block.Right;)
                        {
                            int currWidth = Math.Min(blockWidth, block.Right - x);
                            Rectangle subRect = new Rectangle(x, block.Y, currWidth, block.Height);

                            bool foundChange = false;
                            for (int j = 0; j < subRect.Height; j++)
                            {
                                int blockOffset = ((subRect.Y + j) * stride) + (subRect.X * pixelSize);
                                if (NativeMethods.memcmp(encBuffer + blockOffset, pScan0 + blockOffset, (uint)(currWidth * pixelSize)) != 0)
                                {
                                    foundChange = true;
                                    break;
                                }
                            }

                            if (foundChange)
                            {
                                if (finalUpdates.Count > 0 && finalUpdates.Last().Right == subRect.Left && finalUpdates.Last().Y == subRect.Y && finalUpdates.Last().Height == subRect.Height)
                                {
                                    var last = finalUpdates.Last();
                                    finalUpdates[finalUpdates.Count - 1] = Rectangle.Union(last, subRect);
                                }
                                else
                                {
                                    finalUpdates.Add(subRect);
                                }
                                // 拷贝新数据到缓冲区
                                for (int j = 0; j < subRect.Height; j++)
                                {
                                    int blockOffset = ((subRect.Y + j) * stride) + (subRect.X * pixelSize);
                                    NativeMethods.memcpy(encBuffer + blockOffset, pScan0 + blockOffset, (uint)(currWidth * pixelSize));
                                }
                            }
                            x += currWidth;
                        }
                    }
                }

                // 先写长度占位
                long oldPos = outStream.CanSeek ? outStream.Position : 0;
                outStream.Write(new byte[4], 0, 4);
                int totalDataLen = 0;

                // 编码所有变化块
                foreach (var rect in finalUpdates)
                {
                    using (var tmpBmp = new Bitmap(rect.Width, rect.Height, Format))
                    {
                        var data = tmpBmp.LockBits(new Rectangle(0, 0, rect.Width, rect.Height), ImageLockMode.WriteOnly, Format);
                        try
                        {
                            for (int j = 0; j < rect.Height; j++)
                            {
                                int srcOffset = ((rect.Y + j) * stride) + (rect.X * pixelSize);
                                byte* srcRow = (byte*)Scan0 + srcOffset;
                                byte* dstRow = (byte*)data.Scan0 + j * data.Stride;
                                MemCpy(dstRow, srcRow, rect.Width * pixelSize);
                            }
                        }
                        finally { tmpBmp.UnlockBits(data); }

                        // 坐标和尺寸
                        outStream.Write(BitConverter.GetBytes(rect.X), 0, 4);
                        outStream.Write(BitConverter.GetBytes(rect.Y), 0, 4);
                        outStream.Write(BitConverter.GetBytes(rect.Width), 0, 4);
                        outStream.Write(BitConverter.GetBytes(rect.Height), 0, 4);
                        outStream.Write(new byte[4], 0, 4); // 长度占位

                        long lengthPos = outStream.CanSeek ? outStream.Position : 0;

                        // 压缩编码
                        long before = outStream.CanSeek ? outStream.Position : 0;
                        if (UseJPEG)
                            base.jpgCompression.Compress(tmpBmp, ref outStream);
                        else
                            base.lzwCompression.Compress(tmpBmp, outStream);
                        long after = outStream.CanSeek ? outStream.Position : 0;
                        int blockLen = (int)(after - before);

                        if (outStream.CanSeek)
                        {
                            long currPos = outStream.Position;
                            outStream.Position = lengthPos - 4;
                            outStream.Write(BitConverter.GetBytes(blockLen), 0, 4);
                            outStream.Position = currPos;
                        }
                        totalDataLen += blockLen + 20;
                    }
                }

                // 回写总长度
                if (outStream.CanSeek)
                {
                    long curr = outStream.Position;
                    outStream.Position = oldPos;
                    outStream.Write(BitConverter.GetBytes(totalDataLen), 0, 4);
                    outStream.Position = curr;
                }

                changedBlocks.Clear();
                finalUpdates.Clear();
            }
        }

        public override unsafe Bitmap DecodeData(IntPtr CodecBuffer, uint Length)
        {
            if (Length < 4)
                return null; // 返回null更合理

            int dataSize = *(int*)CodecBuffer;

            // 检查数据完整性
            if (Length < 4 + dataSize || dataSize <= 0)
                return null;

            byte[] temp = new byte[dataSize];

            // 指针偏移安全处理
            IntPtr dataPtr = IntPtr.Add(CodecBuffer, 4);
            Marshal.Copy(dataPtr, temp, 0, dataSize);

            try
            {
                using (var ms = new MemoryStream(temp))
                {
                    var bmp = (Bitmap)Bitmap.FromStream(ms);
                    // 你可以选择释放 decodedBitmap
                    decodedBitmap?.Dispose();
                    decodedBitmap = bmp;
                    return bmp;
                }
            }
            catch
            {
                // 解码失败
                return null;
            }
        }

        public override Bitmap DecodeData(Stream inStream)
        {
            try
            {
                // 读取头部4字节，获得数据区长度
                byte[] temp = ReadExactly(inStream, 4);
                if (temp == null) return null;
                int dataSize = BitConverter.ToInt32(temp, 0);

                if (decodedBitmap == null)
                {
                    // 首次解码，直接读整张图片
                    byte[] imgData = ReadExactly(inStream, dataSize);
                    if (imgData == null) return null;
                    using (var ms = new MemoryStream(imgData))
                    {
                        decodedBitmap = (Bitmap)Bitmap.FromStream(ms);
                    }
                    return decodedBitmap;
                }

                using (Graphics g = Graphics.FromImage(decodedBitmap))
                {
                    int remain = dataSize;
                    while (remain > 0)
                    {
                        // 读取块头信息（坐标、宽高、更新长度）
                        byte[] tempData = ReadExactly(inStream, 4 * 5);
                        if (tempData == null) return null;

                        Rectangle rect = new Rectangle(
                            BitConverter.ToInt32(tempData, 0),
                            BitConverter.ToInt32(tempData, 4),
                            BitConverter.ToInt32(tempData, 8),
                            BitConverter.ToInt32(tempData, 12)
                        );
                        int updateLen = BitConverter.ToInt32(tempData, 16);

                        // 读取图块数据
                        byte[] buffer = ReadExactly(inStream, updateLen);
                        if (buffer == null) return null;

                        onDecodeDebugScan?.Invoke(rect);

                        using (var m = new MemoryStream(buffer))
                        using (var tmp = (Bitmap)Image.FromStream(m))
                        {
                            g.DrawImage(tmp, rect.Location);
                        }

                        remain -= updateLen + (4 * 5);
                    }
                }
                return decodedBitmap;
            }
            catch (Exception ex)
            {
                // 建议开发调试期输出异常
                System.Diagnostics.Debug.WriteLine("DecodeData异常: " + ex);
                return null;
            }
        }

        /// <summary>
        /// 从流中精确读取指定字节数，否则返回 null
        /// </summary>
        private static byte[] ReadExactly(Stream stream, int count)
        {
            byte[] buf = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buf, offset, count - offset);
                if (read == 0) return null; // 流已结束
                offset += read;
            }
            return buf;
        }
    }
}