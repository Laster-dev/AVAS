using System;
using System.IO;
using System.IO.Compression;

namespace MPLib.MP
{
    public static class Zip
    {
        public static byte[] Decompress(byte[] input)
        {
            using (var source = new MemoryStream(input))
            {
                byte[] lengthBytes = new byte[4];
                source.Read(lengthBytes, 0, 4);
                int length = BitConverter.ToInt32(lengthBytes, 0);

                using (var decompressionStream = new DeflateStream(source, CompressionMode.Decompress, true))
                {
                    var buffer = new byte[length];
                    int offset = 0;
                    int read;
                    while ((read = decompressionStream.Read(buffer, offset, length - offset)) > 0)
                        offset += read;
                    return buffer;
                }
            }
        }

        public static byte[] Compress(byte[] input)
        {
            using (var result = new MemoryStream())
            {
                result.Write(BitConverter.GetBytes(input.Length), 0, 4);
                using (var compressionStream = new DeflateStream(result, CompressionMode.Compress, true))
                {
                    compressionStream.Write(input, 0, input.Length);
                }
                return result.ToArray();
            }
        }
    }
}
