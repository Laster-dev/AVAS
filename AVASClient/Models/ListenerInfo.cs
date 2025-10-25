using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AVASClient.Models
{
    public class ListenerInfo : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion

        private string _type = string.Empty;
        /// <summary>
        /// 监听器类型 (TCP, UDP, WebSocket, ICMP, DHCP, HTTP, SMB, Client)
        /// </summary>
        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        private string _bindAddress = string.Empty;
        /// <summary>
        /// 绑定地址
        /// </summary>
        public string BindAddress
        {
            get => _bindAddress;
            set => SetProperty(ref _bindAddress, value);
        }

        private int _port;
        /// <summary>
        /// 端口号
        /// </summary>
        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        private bool _isRunning;
        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }

        /// <summary>
        /// 显示名称 (类型:地址:端口)
        /// </summary>
        public string DisplayName => $"{Type}:{BindAddress}:{Port}";

        /// <summary>
        /// 状态文本
        /// </summary>
        public string StatusText => IsRunning ? "运行中" : "已停止";
    }
}
