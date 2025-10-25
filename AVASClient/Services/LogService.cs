using AVASClient.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using Avalonia.Threading;

namespace AVASClient.Services
{
    public class LogService
    {
        private static LogService? _instance;
        private static readonly object _lock = new object();
        private readonly ObservableCollection<LogEntry> _logs;
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
        private readonly Timer _flushTimer;
        private readonly StringBuilder _logBuffer = new StringBuilder();
        private readonly object _bufferLock = new object();

        public static LogService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new LogService();
                    }
                }
                return _instance;
            }
        }

        public ObservableCollection<LogEntry> Logs => _logs;

        public event Action<LogEntry>? LogAdded;

        private LogService()
        {
            _logs = new ObservableCollection<LogEntry>();
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            _logFilePath = Path.Combine(_logDirectory, $"AVASClient_{DateTime.Now:yyyyMMdd}.log");

            // 确保日志目录存在
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // 设置定时刷新到文件（每5秒）
            _flushTimer = new Timer(FlushToFile, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            // 添加启动日志
            Info("LogService initialized", "LogService");
        }

        public void Debug(string message, string source = "")
        {
            AddLog(LogLevel.Debug, message, source);
        }

        public void Info(string message, string source = "")
        {
            AddLog(LogLevel.Info, message, source);
        }

        public void Warning(string message, string source = "")
        {
            AddLog(LogLevel.Warning, message, source);
        }

        public void Error(string message, string source = "", Exception? exception = null)
        {
            AddLog(LogLevel.Error, message, source, exception);
        }

        public void Fatal(string message, string source = "", Exception? exception = null)
        {
            AddLog(LogLevel.Fatal, message, source, exception);
        }

        private void AddLog(LogLevel level, string message, string source = "", Exception? exception = null)
        {
            var logEntry = new LogEntry(level, message, source, exception);

            // 在 UI 线程上添加日志到集合
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _logs.Add(logEntry);
                
                // 限制日志数量，避免内存泄漏（保留最近1000条）
                if (_logs.Count > 1000)
                {
                    _logs.RemoveAt(0);
                }

                LogAdded?.Invoke(logEntry);
            });

            // 异步写入文件
            _ = Task.Run(async () => await WriteToFileAsync(logEntry));
        }

        private async Task WriteToFileAsync(LogEntry logEntry)
        {
            await _fileLock.WaitAsync();
            try
            {
                var logLine = logEntry.ToFormattedString() + Environment.NewLine;
                
                lock (_bufferLock)
                {
                    _logBuffer.Append(logLine);
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async void FlushToFile(object? state)
        {
            await _fileLock.WaitAsync();
            try
            {
                string content;
                lock (_bufferLock)
                {
                    if (_logBuffer.Length == 0) return;
                    content = _logBuffer.ToString();
                    _logBuffer.Clear();
                }

                await File.AppendAllTextAsync(_logFilePath, content, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // 如果写入文件失败，至少输出到控制台
                Console.WriteLine($"Failed to write log to file: {ex.Message}");
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public void ClearLogs()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _logs.Clear();
            });
        }

        public async Task FlushAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                string content;
                lock (_bufferLock)
                {
                    content = _logBuffer.ToString();
                    _logBuffer.Clear();
                }

                if (!string.IsNullOrEmpty(content))
                {
                    await File.AppendAllTextAsync(_logFilePath, content, Encoding.UTF8);
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public void Dispose()
        {
            _flushTimer?.Dispose();
            _fileLock?.Dispose();
        }
    }
}
