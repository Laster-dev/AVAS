using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace MPLib.MP
{
    unsafe class WriteTools
    {
        
        public static void WriteNull(Stream ms)
        {
            ms.WriteByte(0xC0);
        }

        
        public static void WriteString(Stream ms, String strVal)
        {
            byte[] rawBytes = BytesTools.GetUtf8Bytes(strVal);
            int len = rawBytes.Length;
            
            if (len <= 31)
            {
                ms.WriteByte((byte)(0xA0 + len));
            }
            else if (len <= 255)
            {
                ms.WriteByte(0xD9);
                ms.WriteByte((byte)len);
            }
            else if (len <= 65535)
            {
                ms.WriteByte(0xDA);
                // 直接写入大端序的16位长度
                ms.WriteByte((byte)(len >> 8));
                ms.WriteByte((byte)(len & 0xFF));
            }
            else
            {
                ms.WriteByte(0xDB);
                // 直接写入大端序的32位长度
                ms.WriteByte((byte)(len >> 24));
                ms.WriteByte((byte)((len >> 16) & 0xFF));
                ms.WriteByte((byte)((len >> 8) & 0xFF));
                ms.WriteByte((byte)(len & 0xFF));
            }
            ms.Write(rawBytes, 0, rawBytes.Length);
        }
        
        public static void WriteBinary(Stream ms, byte[] rawBytes)
        {
            int len = rawBytes.Length;
            if (len <= 255)
            {
                ms.WriteByte(0xC4);
                ms.WriteByte((byte)len);
            }
            else if (len <= 65535)
            {
                ms.WriteByte(0xC5);
                // 直接写入大端序的16位长度
                ms.WriteByte((byte)(len >> 8));
                ms.WriteByte((byte)(len & 0xFF));
            }
            else
            {
                ms.WriteByte(0xC6);
                // 直接写入大端序的32位长度
                ms.WriteByte((byte)(len >> 24));
                ms.WriteByte((byte)((len >> 16) & 0xFF));
                ms.WriteByte((byte)((len >> 8) & 0xFF));
                ms.WriteByte((byte)(len & 0xFF));
            }
            ms.Write(rawBytes, 0, rawBytes.Length);
        }

        
        public static void WriteFloat(Stream ms, Double fVal)
        {
            ms.WriteByte(0xCB);
            // 使用unsafe直接写入大端序double
            byte* ptr = (byte*)&fVal;
            ms.WriteByte(ptr[7]);
            ms.WriteByte(ptr[6]);
            ms.WriteByte(ptr[5]);
            ms.WriteByte(ptr[4]);
            ms.WriteByte(ptr[3]);
            ms.WriteByte(ptr[2]);
            ms.WriteByte(ptr[1]);
            ms.WriteByte(ptr[0]);
        }

        
        public static void WriteSingle(Stream ms, Single fVal)
        {
            ms.WriteByte(0xCA);
            // 使用unsafe直接写入大端序float
            byte* ptr = (byte*)&fVal;
            ms.WriteByte(ptr[3]);
            ms.WriteByte(ptr[2]);
            ms.WriteByte(ptr[1]);
            ms.WriteByte(ptr[0]);
        }

        
        public static void WriteBoolean(Stream ms, Boolean bVal)
        {
            ms.WriteByte(bVal ? (byte)0xC3 : (byte)0xC2);
        }

        
        public static void WriteUInt64(Stream ms, UInt64 iVal)
        {
            ms.WriteByte(0xCF);
            // 使用unsafe直接写入大端序UInt64
            byte* ptr = (byte*)&iVal;
            ms.WriteByte(ptr[7]);
            ms.WriteByte(ptr[6]);
            ms.WriteByte(ptr[5]);
            ms.WriteByte(ptr[4]);
            ms.WriteByte(ptr[3]);
            ms.WriteByte(ptr[2]);
            ms.WriteByte(ptr[1]);
            ms.WriteByte(ptr[0]);
        }

        
        public static void WriteInteger(Stream ms, Int64 iVal)
        {
            if (iVal >= 0)
            {   // 正数
                if (iVal <= 127)
                {
                    ms.WriteByte((byte)iVal);
                }
                else if (iVal <= 255)
                {  //UInt8
                    ms.WriteByte(0xCC);
                    ms.WriteByte((byte)iVal);
                }
                else if (iVal <= 0xFFFF)
                {  //UInt16
                    ms.WriteByte(0xCD);
                    // 直接写入大端序16位
                    ms.WriteByte((byte)(iVal >> 8));
                    ms.WriteByte((byte)(iVal & 0xFF));
                }
                else if (iVal <= 0xFFFFFFFF)
                {  //UInt32
                    ms.WriteByte(0xCE);
                    // 直接写入大端序32位
                    ms.WriteByte((byte)(iVal >> 24));
                    ms.WriteByte((byte)((iVal >> 16) & 0xFF));
                    ms.WriteByte((byte)((iVal >> 8) & 0xFF));
                    ms.WriteByte((byte)(iVal & 0xFF));
                }
                else
                {  //Int64
                    ms.WriteByte(0xD3);
                    // 使用unsafe直接写入大端序64位
                    byte* ptr = (byte*)&iVal;
                    ms.WriteByte(ptr[7]);
                    ms.WriteByte(ptr[6]);
                    ms.WriteByte(ptr[5]);
                    ms.WriteByte(ptr[4]);
                    ms.WriteByte(ptr[3]);
                    ms.WriteByte(ptr[2]);
                    ms.WriteByte(ptr[1]);
                    ms.WriteByte(ptr[0]);
                }
            }
            else
            {  // 负数
                if (iVal <= Int32.MinValue)  // 64 bit
                {
                    ms.WriteByte(0xD3);
                    // 使用unsafe直接写入大端序64位
                    byte* ptr = (byte*)&iVal;
                    ms.WriteByte(ptr[7]);
                    ms.WriteByte(ptr[6]);
                    ms.WriteByte(ptr[5]);
                    ms.WriteByte(ptr[4]);
                    ms.WriteByte(ptr[3]);
                    ms.WriteByte(ptr[2]);
                    ms.WriteByte(ptr[1]);
                    ms.WriteByte(ptr[0]);
                }
                else if (iVal <= Int16.MinValue)   // 32 bit
                {
                    ms.WriteByte(0xD2);
                    // 直接写入大端序32位
                    ms.WriteByte((byte)(iVal >> 24));
                    ms.WriteByte((byte)((iVal >> 16) & 0xFF));
                    ms.WriteByte((byte)((iVal >> 8) & 0xFF));
                    ms.WriteByte((byte)(iVal & 0xFF));
                }
                else if (iVal <= -128)   // 16 bit
                {
                    ms.WriteByte(0xD1);
                    // 直接写入大端序16位
                    ms.WriteByte((byte)(iVal >> 8));
                    ms.WriteByte((byte)(iVal & 0xFF));
                }
                else if (iVal <= -32)
                {
                    ms.WriteByte(0xD0);
                    ms.WriteByte((byte)iVal);
                }
                else
                {
                    ms.WriteByte((byte)iVal);
                }
            }
        }

        /// <summary>
        /// 高性能批量写入字节数组，减少系统调用
        /// </summary>
        
        public static void WriteBytesUnsafe(Stream ms, byte[] buffer, int offset, int count)
        {
            ms.Write(buffer, offset, count);
        }

        /// <summary>
        /// 使用unsafe指针快速写入16位大端序整数
        /// </summary>
        
        public static void WriteInt16BigEndian(Stream ms, short value)
        {
            ms.WriteByte((byte)(value >> 8));
            ms.WriteByte((byte)(value & 0xFF));
        }

        /// <summary>
        /// 使用unsafe指针快速写入32位大端序整数
        /// </summary>
        
        public static void WriteInt32BigEndian(Stream ms, int value)
        {
            ms.WriteByte((byte)(value >> 24));
            ms.WriteByte((byte)((value >> 16) & 0xFF));
            ms.WriteByte((byte)((value >> 8) & 0xFF));
            ms.WriteByte((byte)(value & 0xFF));
        }

        /// <summary>
        /// 使用unsafe指针快速写入64位大端序整数
        /// </summary>
        
        public static void WriteInt64BigEndian(Stream ms, long value)
        {
            byte* ptr = (byte*)&value;
            ms.WriteByte(ptr[7]);
            ms.WriteByte(ptr[6]);
            ms.WriteByte(ptr[5]);
            ms.WriteByte(ptr[4]);
            ms.WriteByte(ptr[3]);
            ms.WriteByte(ptr[2]);
            ms.WriteByte(ptr[1]);
            ms.WriteByte(ptr[0]);
        }

        /// <summary>
        /// 优化的字符串写入方法，避免重复的UTF8编码
        /// </summary>
        
        public static void WriteStringOptimized(Stream ms, String strVal, byte[] utf8Buffer)
        {
            if (string.IsNullOrEmpty(strVal))
            {
                ms.WriteByte(0xA0); // fixstr with length 0
                return;
            }

            int len = System.Text.Encoding.UTF8.GetByteCount(strVal);
            
            if (len <= 31)
            {
                ms.WriteByte((byte)(0xA0 + len));
            }
            else if (len <= 255)
            {
                ms.WriteByte(0xD9);
                ms.WriteByte((byte)len);
            }
            else if (len <= 65535)
            {
                ms.WriteByte(0xDA);
                WriteInt16BigEndian(ms, (short)len);
            }
            else
            {
                ms.WriteByte(0xDB);
                WriteInt32BigEndian(ms, len);
            }

            // 使用提供的缓冲区避免重复分配
            if (utf8Buffer.Length >= len)
            {
                System.Text.Encoding.UTF8.GetBytes(strVal, 0, strVal.Length, utf8Buffer, 0);
                ms.Write(utf8Buffer, 0, len);
            }
            else
            {
                byte[] rawBytes = BytesTools.GetUtf8Bytes(strVal);
                ms.Write(rawBytes, 0, rawBytes.Length);
            }
        }
    }
}