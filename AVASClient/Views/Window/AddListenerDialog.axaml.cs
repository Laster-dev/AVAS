using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AVASClient.Models;
using System;

namespace AVASClient;

public partial class AddListenerDialog : Window
{
    public AddListenerDialog()
    {
        InitializeComponent();
        
        // 监听类型变化，更新描述
        TypeComboBox.SelectionChanged += TypeComboBox_SelectionChanged;
        PortTextBox.TextChanged += PortTextBox_TextChanged;
        
        // 设置初始描述
        UpdateDescription();
    }

    private void TypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateDescription();
    }

    private void PortTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateDescription();
    }

    private void UpdateDescription()
    {
        var type = GetSelectedType();
        var port = PortTextBox.Text ?? "8888";
        var address = BindAddressTextBox.Text ?? "0.0.0.0";
        
        DescriptionTextBox.Text = $"{type} 监听器 - {address}:{port}";
    }

    private string GetSelectedType()
    {
        if (TypeComboBox.SelectedItem is ComboBoxItem item)
        {
            return item.Content?.ToString() ?? "TCP";
        }
        return "TCP";
    }

    private void OKButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(BindAddressTextBox.Text) || string.IsNullOrEmpty(PortTextBox.Text))
            {
                return;
            }

            var port = int.Parse(PortTextBox.Text);
            if (port <= 0 || port > 65535)
            {
                return;
            }

            var listener = new ListenerInfo
            {
                Type = GetSelectedType(),
                BindAddress = BindAddressTextBox.Text,
                Port = port,
                IsRunning = false
            };

            Close(listener);
        }
        catch
        {
            // 输入无效，不关闭对话框
        }
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
