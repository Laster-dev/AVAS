using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AVASClient.MessagePackLib;
using AVASClient.Services;
using System;

namespace AVASClient;

public partial class Form_Shell : Window
{
    public string BID { get; set; }
    private const string DLLINFO = "Shell";
    private bool _shellStarted = false;
    private string _currentShellType = ""; // cmd | powershell

    // 无参构造用于 XAML 设计器/反射创建
    public Form_Shell()
    {
        InitializeComponent();
    }

    public Form_Shell(string bid)
    {
        BID = bid;
        InitializeComponent();
        // 事件在 XAML 中通过 Click/Checked/Unchecked/KeyDown 绑定
        this.Closed += OnClosed;
    }

    private string GetShellType()
    {
        var item = CmbShellType.SelectedItem as ComboBoxItem;
        var text = (item?.Content as string)?.Trim() ?? "CMD";
        return string.Equals(text, "PowerShell", StringComparison.OrdinalIgnoreCase) ? "powershell" : "cmd";
    }

    private void AppendOutput(string text)
    {
        TxtOutput.Text += text;
        TxtOutput.CaretIndex = TxtOutput.Text?.Length ?? 0;
    }

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            string text = TxtInput.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return;

            string desiredShell = GetShellType();

            // 首次或切换 Shell 时，自动（重）启动
            if (!_shellStarted || !string.Equals(_currentShellType, desiredShell, StringComparison.OrdinalIgnoreCase))
            {
                // 若已启动且类型不同，先发送停止
                if (_shellStarted && !string.IsNullOrEmpty(_currentShellType) && !_currentShellType.Equals(desiredShell, StringComparison.OrdinalIgnoreCase))
                {
                    ClientMsgPack stop = new ClientMsgPack();
                    stop.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
                    stop.ForcePathObject("BID").SetAsString(BID);
                    stop.ForcePathObject("Pac_ket").AsString = "Stop";
                    Settings.client?.SendFramed(stop.Encode2Bytes());
                }

                _shellStarted = true;
                _currentShellType = desiredShell;
                ClientMsgPack start = new ClientMsgPack();
                start.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
                start.ForcePathObject("BID").SetAsString(BID);
                start.ForcePathObject("Pac_ket").AsString = "Start";
                start.ForcePathObject("ShellType").AsString = desiredShell;
                Settings.client?.SendFramed(start.Encode2Bytes());
            }

            ClientMsgPack msg = new ClientMsgPack();
            msg.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
            msg.ForcePathObject("BID").SetAsString(BID);
            msg.ForcePathObject("Pac_ket").AsString = "Input";
            msg.ForcePathObject("Text").AsString = text;
            Settings.client?.SendFramed(msg.Encode2Bytes());

            TxtInput.Text = string.Empty;
        }
        catch (Exception ex)
        {
            LogService.Instance?.Error($"发送命令失败: {ex.Message}", "Form_Shell");
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        TxtOutput.Text = string.Empty;
    }

    private void OnWrapChecked(object? sender, RoutedEventArgs e)
    {
        TxtOutput.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
    }

    private void OnWrapUnchecked(object? sender, RoutedEventArgs e)
    {
        TxtOutput.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
    }

    private void OnInputKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            OnSendClick(sender, e);
            e.Handled = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        try
        {
            if (_shellStarted)
            {
                ClientMsgPack msg = new ClientMsgPack();
                msg.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
                msg.ForcePathObject("BID").SetAsString(BID);
                msg.ForcePathObject("Pac_ket").AsString = "Stop";
                Settings.client?.SendFramed(msg.Encode2Bytes());
            }
        }
        catch { }
    }
}