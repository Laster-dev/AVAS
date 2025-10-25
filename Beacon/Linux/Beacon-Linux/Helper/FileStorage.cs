using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Beacon.Helper
{
    /// <summary>
    /// Linux兼容的文件存储类，用于替换Windows注册表功能
    /// </summary>
    public static class FileStorage
    {
        private static readonly string StorageDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".beacon", Settings.Hw_id);
        private static readonly object _lock = new object();

        static FileStorage()
        {
            // 确保存储目录存在
            if (!Directory.Exists(StorageDirectory))
            {
                Directory.CreateDirectory(StorageDirectory);
            }
        }

        /// <summary>
        /// 设置二进制值
        /// </summary>
        /// <param name="name">键名</param>
        /// <param name="value">二进制值</param>
        /// <returns>是否成功</returns>
        public static bool SetValue(string name, byte[] value)
        {
            try
            {
                lock (_lock)
                {
                    string filePath = Path.Combine(StorageDirectory, $"{name}.bin");
                    File.WriteAllBytes(filePath, value);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileStorage] 设置值失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取二进制值
        /// </summary>
        /// <param name="name">键名</param>
        /// <returns>二进制值，如果不存在则返回null</returns>
        public static byte[] GetValue(string name)
        {
            try
            {
                lock (_lock)
                {
                    string filePath = Path.Combine(StorageDirectory, $"{name}.bin");
                    if (File.Exists(filePath))
                    {
                        return File.ReadAllBytes(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileStorage] 获取值失败: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 删除值
        /// </summary>
        /// <param name="name">键名</param>
        /// <returns>是否成功</returns>
        public static bool DeleteValue(string name)
        {
            try
            {
                lock (_lock)
                {
                    string filePath = Path.Combine(StorageDirectory, $"{name}.bin");
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileStorage] 删除值失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 删除所有存储的数据
        /// </summary>
        /// <returns>是否成功</returns>
        public static bool DeleteAll()
        {
            try
            {
                lock (_lock)
                {
                    if (Directory.Exists(StorageDirectory))
                    {
                        Directory.Delete(StorageDirectory, true);
                        Directory.CreateDirectory(StorageDirectory);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileStorage] 删除所有数据失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 获取字符串值
        /// </summary>
        /// <param name="name">键名</param>
        /// <returns>字符串值，如果不存在则返回null</returns>
        public static string GetStringValue(string name)
        {
            try
            {
                lock (_lock)
                {
                    string filePath = Path.Combine(StorageDirectory, $"{name}.txt");
                    if (File.Exists(filePath))
                    {
                        return File.ReadAllText(filePath, Encoding.UTF8);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileStorage] 获取字符串值失败: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 设置字符串值
        /// </summary>
        /// <param name="name">键名</param>
        /// <param name="value">字符串值</param>
        /// <returns>是否成功</returns>
        public static bool SetStringValue(string name, string value)
        {
            try
            {
                lock (_lock)
                {
                    string filePath = Path.Combine(StorageDirectory, $"{name}.txt");
                    File.WriteAllText(filePath, value, Encoding.UTF8);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FileStorage] 设置字符串值失败: {ex.Message}");
                return false;
            }
        }
    }
}
