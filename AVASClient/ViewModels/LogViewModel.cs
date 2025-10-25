using AVASClient.Models;
using AVASClient.Services;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AVASClient.ViewModels
{
    public class LogViewModel : INotifyPropertyChanged
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

        private readonly LogService _logService;
        private ObservableCollection<LogEntry> _filteredLogs;
        private LogLevel _selectedLogLevel = LogLevel.Debug;
        private string _searchText = string.Empty;
        private bool _autoScroll = true;

        public ObservableCollection<LogEntry> FilteredLogs
        {
            get => _filteredLogs;
            set => SetProperty(ref _filteredLogs, value);
        }

        public LogLevel SelectedLogLevel
        {
            get => _selectedLogLevel;
            set
            {
                if (SetProperty(ref _selectedLogLevel, value))
                {
                    ApplyFilters();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilters();
                }
            }
        }

        public bool AutoScroll
        {
            get => _autoScroll;
            set => SetProperty(ref _autoScroll, value);
        }

        public ICommand ClearLogsCommand { get; }
        public ICommand ExportLogsCommand { get; }
        public ICommand CopyLogCommand { get; }

        public LogViewModel()
        {
            _logService = LogService.Instance;
            _filteredLogs = new ObservableCollection<LogEntry>();

            // 订阅日志添加事件
            _logService.LogAdded += OnLogAdded;

            // 初始化命令
            ClearLogsCommand = new RelayCommand(ClearLogs);
            ExportLogsCommand = new RelayCommand(ExportLogs);
            CopyLogCommand = new RelayCommand<LogEntry>(CopyLog);

            // 加载现有日志
            LoadExistingLogs();
        }

        private void OnLogAdded(LogEntry logEntry)
        {
            if (ShouldIncludeLog(logEntry))
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    FilteredLogs.Add(logEntry);
                });
            }
        }

        private void LoadExistingLogs()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                FilteredLogs.Clear();
                foreach (var log in _logService.Logs)
                {
                    if (ShouldIncludeLog(log))
                    {
                        FilteredLogs.Add(log);
                    }
                }
            });
        }

        private bool ShouldIncludeLog(LogEntry log)
        {
            // 检查日志级别
            if (log.Level < SelectedLogLevel)
                return false;

            // 检查搜索文本
            if (!string.IsNullOrEmpty(SearchText))
            {
                var searchLower = SearchText.ToLower();
                return log.Message.ToLower().Contains(searchLower) ||
                       log.Source.ToLower().Contains(searchLower) ||
                       log.Level.ToString().ToLower().Contains(searchLower);
            }

            return true;
        }

        private void ApplyFilters()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                FilteredLogs.Clear();
                foreach (var log in _logService.Logs)
                {
                    if (ShouldIncludeLog(log))
                    {
                        FilteredLogs.Add(log);
                    }
                }
            });
        }

        private void ClearLogs()
        {
            _logService.ClearLogs();
            FilteredLogs.Clear();
        }

        private async void ExportLogs()
        {
            try
            {
                var fileName = $"AVASClient_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                
                var logs = string.Join(Environment.NewLine, FilteredLogs.Select(log => log.ToFormattedString()));
                await System.IO.File.WriteAllTextAsync(filePath, logs);
                
                _logService.Info($"Logs exported to: {filePath}", "LogViewModel");
            }
            catch (Exception ex)
            {
                _logService.Error("Failed to export logs", "LogViewModel", ex);
            }
        }

        private void CopyLog(LogEntry? logEntry)
        {
            if (logEntry != null)
            {
                try
                {
                    // 这里需要实现剪贴板功能
                    // 在 Avalonia 中可以使用 Avalonia.Clipboard
                    _logService.Info($"Log copied to clipboard: {logEntry.Message}", "LogViewModel");
                }
                catch (Exception ex)
                {
                    _logService.Error("Failed to copy log", "LogViewModel", ex);
                }
            }
        }

        public void Dispose()
        {
            _logService.LogAdded -= OnLogAdded;
        }
    }

    // 简单的 RelayCommand 实现
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

        public void Execute(object? parameter) => _execute((T?)parameter);
    }
}
