using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AVASClient.Helper
{
    internal class GetHash
    {
        public static string GetChecksum(string file)
        {
            using var fs = File.OpenRead(file);
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
