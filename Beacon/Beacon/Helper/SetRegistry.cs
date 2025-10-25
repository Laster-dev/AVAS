using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Beacon.Helper
{
    public static class SetRegistry
    {
        private static readonly string ID = @"Software\" + Settings.Hw_id;

        public static bool SetValue(string name, byte[] value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(ID, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    key.SetValue(name, value, RegistryValueKind.Binary);
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// 从当前用户注册表指定子键获取指定名称的字节数组（byte[]）值。
        /// </summary>
        /// <param name="value">要获取的值的名称。</param>
        /// <returns>获取到的字节数组，如果出错则返回null。</returns>
        public static byte[] GetValue(string value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(ID))
                {
                    // 从注册表中获取名称为 value 的值
                    object o = key.GetValue(value);

                    return (byte[])o;
                }
            }
            catch
            {
             
            }
            // 如果出错，返回 null
            return null;
        }

        public static bool DeleteValue(string name)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(ID))
                {
                    key.DeleteValue(name);
                    return true;
                }
            }
            catch
            {
                
            }
            return false;
        }

        public static bool DeleteSubKey()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("", true))
                {
                    key.DeleteSubKeyTree(ID);
                    return true;
                }
            }
            catch
            {
             
            }
            return false;
        }

        /// <summary>
        /// 从注册表获取字符串值
        /// </summary>
        /// <param name="name">键名</param>
        /// <returns>字符串值，如果不存在则返回null</returns>
        public static string GetStringValue(string name)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(ID))
                {
                    object value = key.GetValue(name);
                    return value?.ToString();
                }
            }
            catch
            {
               
            }
            return null;
        }

        /// <summary>
        /// 设置字符串值到注册表
        /// </summary>
        /// <param name="name">键名</param>
        /// <param name="value">字符串值</param>
        /// <returns>是否成功</returns>
        public static bool SetStringValue(string name, string value)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(ID, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    key.SetValue(name, value, RegistryValueKind.String);
                    return true;
                }
            }
            catch
            {
              
            }
            return false;
        }
    }
}
