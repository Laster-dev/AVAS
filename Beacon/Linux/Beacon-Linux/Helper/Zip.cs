using System;
using System.IO;
using System.IO.Compression;

namespace Beacon.Helper
{
    /// <summary>
    /// 简单的ZIP压缩/解压缩工具类
    /// </summary>
    public static class Zip
    {
        /// <summary>
        /// 解压缩字节数组
        /// </summary>
        /// <param name="compressedData">压缩的数据</param>
        /// <returns>解压缩后的数据</returns>
        public static byte[] Decompress(byte[] compressedData)
        {
            try
            {
                using (var compressedStream = new MemoryStream(compressedData))
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                using (var decompressedStream = new MemoryStream())
                {
                    gzipStream.CopyTo(decompressedStream);
                    return decompressedStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Zip] 解压缩失败: {ex.Message}");
                return compressedData; // 如果解压缩失败，返回原始数据
            }
        }

        /// <summary>
        /// 压缩字节数组
        /// </summary>
        /// <param name="data">要压缩的数据</param>
        /// <returns>压缩后的数据</returns>
        public static byte[] Compress(byte[] data)
        {
            try
            {
                using (var compressedStream = new MemoryStream())
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    gzipStream.Write(data, 0, data.Length);
                    gzipStream.Close();
                    return compressedStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Zip] 压缩失败: {ex.Message}");
                return data; // 如果压缩失败，返回原始数据
            }
        }
    }
}
