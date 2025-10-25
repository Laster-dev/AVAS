using System;
using System.Text;

namespace MPLib.MP
{
    public static class BytesTools
    {
        private static readonly UTF8Encoding u8 = new UTF8Encoding(false, true);

        public static byte[] GetUtf8Bytes(string s)
        {
            if (string.IsNullOrEmpty(s)) return new byte[0];
            return u8.GetBytes(s);
        }

        public static string GetString(byte[] u)
        {
            if (u == null || u.Length == 0) return string.Empty;
            return u8.GetString(u, 0, u.Length);
        }

        public static string BytesAsString(byte[] b)
        {
            if (b == null || b.Length == 0) return string.Empty;
            StringBuilder sb = new StringBuilder(b.Length * 4);
            unsafe
            {
                fixed (byte* p = b)
                {
                    for (int i = 0; i < b.Length; i++)
                    {
                        sb.Append(((int)p[i]).ToString("D3"));
                        sb.Append(' ');
                    }
                }
            }
            return sb.ToString();
        }

        public static string BytesAsHexString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            StringBuilder sb = new StringBuilder(bytes.Length * 3);
            unsafe
            {
                fixed (byte* p = bytes)
                {
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        sb.Append(p[i].ToString("X2"));
                        sb.Append(' ');
                    }
                }
            }
            return sb.ToString();
        }

        public static byte[] SwapBytes(byte[] v)
        {
            if (v == null) return new byte[0];
            byte[] r = new byte[v.Length];
            unsafe
            {
                fixed (byte* src = v)
                fixed (byte* dst = r)
                {
                    int len = v.Length;
                    for (int i = 0; i < len; i++)
                    {
                        dst[i] = src[len - 1 - i];
                    }
                }
            }
            return r;
        }

        public static byte[] SwapInt64(long v)
        {
            byte[] r = new byte[8];
            unsafe
            {
                byte* pv = (byte*)&v;
                r[0] = pv[7];
                r[1] = pv[6];
                r[2] = pv[5];
                r[3] = pv[4];
                r[4] = pv[3];
                r[5] = pv[2];
                r[6] = pv[1];
                r[7] = pv[0];
            }
            return r;
        }

        public static byte[] SwapInt32(int v)
        {
            byte[] r = new byte[4];
            unsafe
            {
                byte* pv = (byte*)&v;
                r[0] = pv[3];
                r[1] = pv[2];
                r[2] = pv[1];
                r[3] = pv[0];
            }
            return r;
        }

        public static byte[] SwapInt16(short v)
        {
            byte[] r = new byte[2];
            unsafe
            {
                byte* pv = (byte*)&v;
                r[0] = pv[1];
                r[1] = pv[0];
            }
            return r;
        }

        public static byte[] SwapDouble(double v)
        {
            byte[] r = new byte[8];
            unsafe
            {
                byte* pv = (byte*)&v;
                r[0] = pv[7];
                r[1] = pv[6];
                r[2] = pv[5];
                r[3] = pv[4];
                r[4] = pv[3];
                r[5] = pv[2];
                r[6] = pv[1];
                r[7] = pv[0];
            }
            return r;
        }
    }
}
