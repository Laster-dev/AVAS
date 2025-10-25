using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Buffers;

namespace StreamLibrary.src
{
    public class LzwCompression : IDisposable
    {
        private readonly ImageCodecInfo encoderInfo;
        private readonly string mimeType = "image/tiff"; // LZW 仅支持 TIFF
        private bool disposed;

        public LzwCompression()
        {
            encoderInfo = GetEncoderInfo(mimeType) ?? throw new InvalidOperationException("TIFF 编码器不可用");
        }

        public byte[] Compress(Bitmap bmp, byte[] additionInfo = null, long quality = 90L)
        {
            // 预分配大缓冲区，避免多次分配
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024); // 1MB，实际按需求调整
            try
            {
                using (var ms = new MemoryStream(buffer, 0, buffer.Length, true, true))
                {
                    if (additionInfo != null)
                        ms.Write(additionInfo, 0, additionInfo.Length);

                    using (var encParams = GetEncoderParameters(quality))
                    {
                        bmp.Save(ms, encoderInfo, encParams);
                    }
                    var len = (int)ms.Position;
                    byte[] result = new byte[len];
                    Buffer.BlockCopy(buffer, 0, result, 0, len);
                    return result;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public void Compress(Bitmap bmp, Stream stream, byte[] additionInfo = null, long quality = 90L)
        {
            if (additionInfo != null)
                stream.Write(additionInfo, 0, additionInfo.Length);

            using (var encParams = GetEncoderParameters(quality))
            {
                bmp.Save(stream, encoderInfo, encParams);
            }
        }

        private EncoderParameters GetEncoderParameters(long quality)
        {
            var encParams = new EncoderParameters(2);
            encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            encParams.Param[1] = new EncoderParameter(System.Drawing.Imaging.Encoder.Compression, (long)EncoderValue.CompressionLZW);
            return encParams;
        }

        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();
            foreach (var enc in encoders)
                if (enc.MimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase))
                    return enc;
            return null;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
            }
        }
    }
}