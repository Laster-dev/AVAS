using AVASClient.Models;
using Avalonia.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AVASClient.Services
{
    public static class ListenerManager
    {
        private static AvaloniaList<ListenerInfo> _listeners = new();
        
        public static AvaloniaList<ListenerInfo> Listeners => _listeners;

        public static event Action<string>? StatusUpdated;

        public static void UpdateStatus(string message)
        {
            StatusUpdated?.Invoke($"{DateTime.Now:HH:mm:ss} - {message}");
        }

        public static void AddListener(ListenerInfo listener)
        {
            // 检查是否已存在相同的监听器
            var existing = _listeners.FirstOrDefault(l => l.Type == listener.Type && l.BindAddress == listener.BindAddress && l.Port == listener.Port);
            if (existing == null)
            {
                _listeners.Add(listener);
                UpdateStatus($"监听器 {listener.DisplayName} 已添加");
            }
            else
            {
                // 如果已存在，更新状态
                existing.IsRunning = listener.IsRunning;
                UpdateStatus($"监听器 {listener.DisplayName} 状态已更新");
            }
        }

        public static void RemoveListener(ListenerInfo listener)
        {
            var existing = _listeners.FirstOrDefault(l => l.Type == listener.Type && l.BindAddress == listener.BindAddress && l.Port == listener.Port);
            if (existing != null)
            {
                _listeners.Remove(existing);
                UpdateStatus($"监听器 {listener.DisplayName} 已删除");
            }
        }

        public static void UpdateListenerStatus(string type, string bindAddress, int port, bool isRunning)
        {
            var listener = _listeners.FirstOrDefault(l => l.Type == type && l.BindAddress == bindAddress && l.Port == port);
            if (listener != null)
            {
                listener.IsRunning = isRunning;
                UpdateStatus($"监听器 {listener.DisplayName} 状态已更新: {(isRunning ? "运行中" : "已停止")}");
            }
        }

        public static void ClearListeners()
        {
            _listeners.Clear();
        }
    }
}
