using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;

namespace cc.dw.src
{
    /// <summary>
    /// 确定性压缩类 - 解决JPEG压缩随机性问题
    /// </summary>
    public class DeterministicCompression
    {
        private readonly int _quality;
        private readonly bool _useHashComparison;
        private byte[] _lastCompressedData;
        private string _lastImageHash;

        public DeterministicCompression(int quality, bool useHashComparison = true)
        {
            _quality = quality;
            _useHashComparison = useHashComparison;
        }

        /// <summary>
        /// 压缩图像，如果内容相同则返回空数组
        /// </summary>
        public byte[] CompressIfChanged(Bitmap bmp)
        {
            if (bmp == null) return new byte[0];

            // 方案1: 使用图像哈希比较
            if (_useHashComparison)
            {
                string currentHash = CalculateImageHash(bmp);
                if (currentHash == _lastImageHash && _lastCompressedData != null)
                {
                    return new byte[0]; // 图像未变化，返回空数组
                }
                _lastImageHash = currentHash;
            }

            // 方案2: 使用PNG无损压缩作为基准
            byte[] compressedData = CompressWithPNG(bmp);
            
            // 方案3: 如果PNG太大，使用优化的JPEG
            if (compressedData.Length > 50000) // 50KB阈值
            {
                compressedData = CompressWithOptimizedJPEG(bmp);
            }

            _lastCompressedData = compressedData;
            return compressedData;
        }

        /// <summary>
        /// 计算图像内容的MD5哈希
        /// </summary>
        private string CalculateImageHash(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                // 使用PNG格式确保像素级一致性
                bmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                
                using (var md5 = MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(ms);
                    return Convert.ToBase64String(hash);
                }
            }
        }

        /// <summary>
        /// 使用PNG无损压缩
        /// </summary>
        private byte[] CompressWithPNG(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 使用优化的JPEG压缩
        /// </summary>
        private byte[] CompressWithOptimizedJPEG(Bitmap bmp)
        {
            // 创建确定性的JPEG编码器参数
            var encoderInfo = GetEncoderInfo("image/jpeg");
            var encoderParams = new EncoderParameters(3);
            
            // 质量参数
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)_quality);
            
            // 压缩级别 - 使用固定值确保一致性
            encoderParams.Param[1] = new EncoderParameter(Encoder.Compression, (long)2);
            
            // 添加确定性参数
            encoderParams.Param[2] = new EncoderParameter(Encoder.SaveFlag, (long)0);

            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, encoderInfo, encoderParams);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 使用LZW压缩作为替代方案
        /// </summary>
        public byte[] CompressWithLZW(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                // 使用GIF格式，它使用LZW压缩
                bmp.Save(ms, ImageFormat.Gif);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 获取图像编码器信息
        /// </summary>
        private ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] imageEncoders = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < imageEncoders.Length; i++)
            {
                if (imageEncoders[i].MimeType == mimeType)
                {
                    return imageEncoders[i];
                }
            }
            return null;
        }

        public void Dispose()
        {
            _lastCompressedData = null;
            _lastImageHash = null;
        }
    }
}
