using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace MPLib.MP
{
    unsafe class ReadTools
    {
        
        public static String ReadString(Stream ms, int len)
        {
            if (len == 0) return string.Empty;
            byte[] rawBytes = new byte[len];
            ms.Read(rawBytes, 0, len);
            return BytesTools.GetString(rawBytes);
        }

        
        public static String ReadString(Stream ms)
        {
            byte strFlag = (byte)ms.ReadByte();
            return ReadString(strFlag, ms);
        }

        
        public static String ReadString(byte strFlag, Stream ms)
        {
            int len = 0;
            
            if ((strFlag >= 0xA0) && (strFlag <= 0xBF))
            {
                len = strFlag - 0xA0;
            }
            else if (strFlag == 0xD9)
            {
                len = ms.ReadByte();
            }
            else if (strFlag == 0xDA)
            {
                // 直接读取大端序16位长度，避免数组分配
                int b1 = ms.ReadByte();
                int b2 = ms.ReadByte();
                len = (b1 << 8) | b2;
            }
            else if (strFlag == 0xDB)
            {
                // 直接读取大端序32位长度，避免数组分配
                int b1 = ms.ReadByte();
                int b2 = ms.ReadByte();
                int b3 = ms.ReadByte();
                int b4 = ms.ReadByte();
                len = (b1 << 24) | (b2 << 16) | (b3 << 8) | b4;
            }
            
            if (len == 0) return string.Empty;
            
            byte[] rawBytes = new byte[len];
            ms.Read(rawBytes, 0, len);
            return BytesTools.GetString(rawBytes);
        }

        /// <summary>
        /// 使用unsafe指针快速读取16位大端序整数
        /// </summary>
        
        public static short ReadInt16BigEndian(Stream ms)
        {
            int b1 = ms.ReadByte();
            int b2 = ms.ReadByte();
            return (short)((b1 << 8) | b2);
        }

        /// <summary>
        /// 使用unsafe指针快速读取32位大端序整数
        /// </summary>
        
        public static int ReadInt32BigEndian(Stream ms)
        {
            int b1 = ms.ReadByte();
            int b2 = ms.ReadByte();
            int b3 = ms.ReadByte();
            int b4 = ms.ReadByte();
            return (b1 << 24) | (b2 << 16) | (b3 << 8) | b4;
        }

        /// <summary>
        /// 使用unsafe指针快速读取64位大端序整数
        /// </summary>
        
        public static long ReadInt64BigEndian(Stream ms)
        {
            long result = 0;
            for (int i = 0; i < 8; i++)
            {
                result = (result << 8) | (byte)ms.ReadByte();
            }
            return result;
        }

        /// <summary>
        /// 使用unsafe指针快速读取大端序float
        /// </summary>
        
        public static float ReadSingleBigEndian(Stream ms)
        {
            int intValue = ReadInt32BigEndian(ms);
            return *(float*)&intValue;
        }

        /// <summary>
        /// 使用unsafe指针快速读取大端序double
        /// </summary>
        
        public static double ReadDoubleBigEndian(Stream ms)
        {
            long longValue = ReadInt64BigEndian(ms);
            return *(double*)&longValue;
        }

        /// <summary>
        /// 快速读取二进制数据，避免不必要的数组分配
        /// </summary>
        
        public static byte[] ReadBinary(Stream ms, byte binFlag)
        {
            int len = 0;
            
            if (binFlag == 0xC4)
            {
                len = ms.ReadByte();
            }
            else if (binFlag == 0xC5)
            {
                // 直接读取大端序16位长度
                int b1 = ms.ReadByte();
                int b2 = ms.ReadByte();
                len = (b1 << 8) | b2;
            }
            else if (binFlag == 0xC6)
            {
                // 直接读取大端序32位长度
                int b1 = ms.ReadByte();
                int b2 = ms.ReadByte();
                int b3 = ms.ReadByte();
                int b4 = ms.ReadByte();
                len = (b1 << 24) | (b2 << 16) | (b3 << 8) | b4;
            }
            
            if (len == 0) return new byte[0];
            
            byte[] result = new byte[len];
            ms.Read(result, 0, len);
            return result;
        }

        /// <summary>
        /// 快速批量读取字节到缓冲区，减少系统调用
        /// </summary>
        
        public static int ReadBytesToBuffer(Stream ms, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int bytesRead = ms.Read(buffer, offset + totalRead, count - totalRead);
                if (bytesRead == 0) break;
                totalRead += bytesRead;
            }
            return totalRead;
        }
    }
}