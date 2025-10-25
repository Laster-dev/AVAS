using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Collections;
using Avalonia.Threading;
using AVASClient.Models;
using AVASClient.MessagePackLib;
using AVASClient.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AVASClient;

public partial class ServerSettings : Window
{
    private Client? _client;

    public ServerSettings()
    {
        InitializeComponent();
        ListenersListBox.ItemsSource = ListenerManager.Listeners;
        
        // 订阅状态更新事件
        ListenerManager.StatusUpdated += OnStatusUpdated;
        
        // 检查是否已有连接
        CheckExistingConnection();
    }

    private void CheckExistingConnection()
    {
        if (Settings.client != null && Settings.client.IsConnected)
        {
            _client = Settings.client;
            UpdateStatus("使用现有连接");
            // 自动刷新监听器列表
            _ = RefreshListeners();
        }
        else
        {
            UpdateStatus("请先连接服务器");
        }
    }

    private void OnStatusUpdated(string message)
    {
        // 在UI线程上更新状态
        Dispatcher.UIThread.Post(() => {
            StatusTextBlock.Text = message;
        });
    }

    private async void ConnectButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (iptext.Text == null || porttext.Text == null)
            {
                UpdateStatus("请输入服务器地址和端口");
                return;
            }

            UpdateStatus("正在连接服务器...");
            
            Settings.ServerAddress = iptext.Text;
            Settings.ServerPort = int.Parse(porttext.Text);
            
            // 使用全局 Client 实例
            if (Settings.client == null)
            {
                Settings.client = new Client();
            }
            _client = Settings.client;
            
            // 连接服务器
            await Task.Run(() => _client.Connect(Settings.ServerAddress, Settings.ServerPort));
            
            UpdateStatus("服务器连接成功");
            
            // 连接成功后刷新监听器列表
            await RefreshListeners();
        }
        catch (Exception ex)
        {
            UpdateStatus($"连接失败: {ex.Message}");
        }
    }

    private async void RefreshListeners_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RefreshListeners();
    }

    private async void AddListener_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_client == null)
        {
            UpdateStatus("请先连接服务器");
            return;
        }

        // 创建添加监听器对话框
        var dialog = new AddListenerDialog();
        var result = await dialog.ShowDialog<ListenerInfo?>(this);
        
        if (result != null)
        {
            await AddListener(result);
        }
    }

    private async void StartSelected_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selected = ListenersListBox.SelectedItem as ListenerInfo;
        if (selected != null)
        {
            await StartListener(selected);
        }
        else
        {
            UpdateStatus("请选择要启动的监听器");
        }
    }

    private async void StopSelected_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selected = ListenersListBox.SelectedItem as ListenerInfo;
        if (selected != null)
        {
            await StopListener(selected);
        }
        else
        {
            UpdateStatus("请选择要停止的监听器");
        }
    }

    private async void RemoveSelected_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selected = ListenersListBox.SelectedItem as ListenerInfo;
        if (selected != null)
        {
            await RemoveListener(selected);
        }
        else
        {
            UpdateStatus("请选择要删除的监听器");
        }
    }

    private async void StartListener_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ListenerInfo listener)
        {
            await StartListener(listener);
        }
    }

    private async void StopListener_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ListenerInfo listener)
        {
            await StopListener(listener);
        }
    }

    private async void RemoveListener_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ListenerInfo listener)
        {
            await RemoveListener(listener);
        }
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 取消订阅事件
        ListenerManager.StatusUpdated -= OnStatusUpdated;
        this.Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        // 确保在窗口关闭时取消订阅
        ListenerManager.StatusUpdated -= OnStatusUpdated;
        base.OnClosed(e);
    }

    private async Task RefreshListeners()
    {
        if (_client == null)
        {
            UpdateStatus("请先连接服务器");
            return;
        }

        try
        {
            UpdateStatus("正在获取监听器列表...");
            
            // 发送获取活跃监听器请求
            var msgPack = new ClientMsgPack();
            msgPack.ForcePathObject("Pac_ket").SetAsString("ListenerManagement");
            msgPack.ForcePathObject("Action").SetAsString("GET_ACTIVE");
            
            _client.SendFramed(msgPack.Encode2Bytes());
            
            UpdateStatus("监听器列表已刷新");
        }
        catch (Exception ex)
        {
            UpdateStatus($"刷新失败: {ex.Message}");
        }
    }

    private async Task AddListener(ListenerInfo listener)
    {
        if (_client == null) return;

        try
        {
            UpdateStatus($"正在添加监听器 {listener.DisplayName}...");
            
            var msgPack = new ClientMsgPack();
            msgPack.ForcePathObject("Pac_ket").SetAsString("ListenerManagement");
            msgPack.ForcePathObject("Action").SetAsString("ADD");
            msgPack.ForcePathObject("Type").SetAsString(listener.Type);
            msgPack.ForcePathObject("BindAddress").SetAsString(listener.BindAddress);
            msgPack.ForcePathObject("Port").SetAsInteger(listener.Port);
            
            _client.SendFramed(msgPack.Encode2Bytes());
            
            // 等待服务器响应，不直接添加到列表
            UpdateStatus($"已发送添加监听器请求: {listener.DisplayName}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"添加监听器失败: {ex.Message}");
        }
    }

    private async Task StartListener(ListenerInfo listener)
    {
        if (_client == null) return;

        try
        {
            UpdateStatus($"正在启动监听器 {listener.DisplayName}...");
            
            var msgPack = new ClientMsgPack();
            msgPack.ForcePathObject("Pac_ket").SetAsString("ListenerManagement");
            msgPack.ForcePathObject("Action").SetAsString("START");
            msgPack.ForcePathObject("Type").SetAsString(listener.Type);
            msgPack.ForcePathObject("BindAddress").SetAsString(listener.BindAddress);
            msgPack.ForcePathObject("Port").SetAsInteger(listener.Port);
            
            _client.SendFramed(msgPack.Encode2Bytes());
            
            // 等待服务器响应，不直接修改状态
            UpdateStatus($"已发送启动监听器请求: {listener.DisplayName}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"启动监听器失败: {ex.Message}");
        }
    }

    private async Task StopListener(ListenerInfo listener)
    {
        if (_client == null) return;

        try
        {
            UpdateStatus($"正在停止监听器 {listener.DisplayName}...");
            
            var msgPack = new ClientMsgPack();
            msgPack.ForcePathObject("Pac_ket").SetAsString("ListenerManagement");
            msgPack.ForcePathObject("Action").SetAsString("STOP");
            msgPack.ForcePathObject("Type").SetAsString(listener.Type);
            msgPack.ForcePathObject("BindAddress").SetAsString(listener.BindAddress);
            msgPack.ForcePathObject("Port").SetAsInteger(listener.Port);
            
            _client.SendFramed(msgPack.Encode2Bytes());
            
            // 等待服务器响应，不直接修改状态
            UpdateStatus($"已发送停止监听器请求: {listener.DisplayName}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"停止监听器失败: {ex.Message}");
        }
    }

    private async Task RemoveListener(ListenerInfo listener)
    {
        if (_client == null) return;

        try
        {
            UpdateStatus($"正在删除监听器 {listener.DisplayName}...");
            
            var msgPack = new ClientMsgPack();
            msgPack.ForcePathObject("Pac_ket").SetAsString("ListenerManagement");
            msgPack.ForcePathObject("Action").SetAsString("REMOVE");
            msgPack.ForcePathObject("Type").SetAsString(listener.Type);
            msgPack.ForcePathObject("BindAddress").SetAsString(listener.BindAddress);
            msgPack.ForcePathObject("Port").SetAsInteger(listener.Port);
            
            _client.SendFramed(msgPack.Encode2Bytes());
            
            // 等待服务器响应，不直接删除
            UpdateStatus($"已发送删除监听器请求: {listener.DisplayName}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"删除监听器失败: {ex.Message}");
        }
    }

    private void UpdateStatus(string message)
    {
        StatusTextBlock.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
    }
}