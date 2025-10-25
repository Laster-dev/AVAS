using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using TeamServer.Config;

namespace TeamServer.Services
{
    /// <summary>
    /// 日志服务 - 统一的日志管理
    /// </summary>
    public class LoggerService
    {
        private static LoggerService? _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// 单例实例
        /// </summary>
        public static LoggerService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LoggerService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 当前日志级别
        /// </summary>
        public LogLevel CurrentLogLevel { get; set; } = LogLevel.Info;

        /// <summary>
        /// 是否启用控制台输出
        /// </summary>
        public bool EnableConsoleOutput { get; set; } = true;

        /// <summary>
        /// 是否启用文件输出
        /// </summary>
        public bool EnableFileOutput { get; set; } = true;

        /// <summary>
        /// 是否启用彩色输出
        /// </summary>
        public bool EnableColorOutput { get; set; } = true;

        /// <summary>
        /// 是否显示时间戳
        /// </summary>
        public bool ShowTimestamp { get; set; } = true;

        /// <summary>
        /// 是否显示模块名
        /// </summary>
        public bool ShowModule { get; set; } = true;

        /// <summary>
        /// 是否显示会话信息
        /// </summary>
        public bool ShowSession { get; set; } = true;

        /// <summary>
        /// 日志文件路径
        /// </summary>
        public string LogFilePath { get; set; } = "logs/teamserver.log";

        /// <summary>
        /// 日志队列
        /// </summary>
        private readonly ConcurrentQueue<LogEntry> _logQueue = new();

        /// <summary>
        /// 是否正在运行
        /// </summary>
        private bool _isRunning = false;

        /// <summary>
        /// 取消令牌源
        /// </summary>
        private CancellationTokenSource? _cancellationTokenSource;

        private LoggerService()
        {
            // 使用默认设置，避免循环依赖
            // 配置将在 AppConfig.ApplyLogConfig() 中应用
        }

        /// <summary>
        /// 从配置加载设置（已移除，避免循环依赖）
        /// 配置现在通过 ApplyLogConfig() 方法应用
        /// </summary>
        private void LoadSettings()
        {
            // 使用默认设置
            CurrentLogLevel = LogLevel.Info;
            EnableConsoleOutput = true;
            EnableFileOutput = true;
            EnableColorOutput = true;
            ShowTimestamp = true;
            ShowModule = true;
            ShowSession = true;
        }

        /// <summary>
        /// 启动日志服务
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            // 确保日志目录存在
            if (EnableFileOutput)
            {
                var logDir = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
            }

            // 启动后台日志处理任务
            _ = Task.Run(ProcessLogQueue);
        }

        /// <summary>
        /// 停止日志服务
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">消息</param>
        /// <param name="module">模块名</param>
        /// <param name="exception">异常</param>
        /// <param name="sessionId">会话ID</param>
        /// <param name="clientIP">客户端IP</param>
        /// <param name="protocol">协议类型</param>
        public void Log(LogLevel level, string message, string? module = null, Exception? exception = null, 
            string? sessionId = null, string? clientIP = null, string? protocol = null)
        {
            // 检查日志级别
            if (level < CurrentLogLevel) return;

            var entry = new LogEntry(level, message, exception, module)
            {
                SessionId = sessionId,
                ClientIP = clientIP,
                Protocol = protocol
            };

            _logQueue.Enqueue(entry);
        }

        /// <summary>
        /// 记录调试日志
        /// </summary>
        public void Debug(string message, string? module = null, string? sessionId = null, string? clientIP = null, string? protocol = null)
        {
            Log(LogLevel.Debug, message, module, null, sessionId, clientIP, protocol);
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        public void Info(string message, string? module = null, string? sessionId = null, string? clientIP = null, string? protocol = null)
        {
            Log(LogLevel.Info, message, module, null, sessionId, clientIP, protocol);
        }

        /// <summary>
        /// 记录成功日志
        /// </summary>
        public void Success(string message, string? module = null, string? sessionId = null, string? clientIP = null, string? protocol = null)
        {
            Log(LogLevel.Success, message, module, null, sessionId, clientIP, protocol);
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        public void Warning(string message, string? module = null, Exception? exception = null, string? sessionId = null, string? clientIP = null, string? protocol = null)
        {
            Log(LogLevel.Warning, message, module, exception, sessionId, clientIP, protocol);
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        public void Error(string message, string? module = null, Exception? exception = null, string? sessionId = null, string? clientIP = null, string? protocol = null)
        {
            Log(LogLevel.Error, message, module, exception, sessionId, clientIP, protocol);
        }

        /// <summary>
        /// 记录严重错误日志
        /// </summary>
        public void Critical(string message, string? module = null, Exception? exception = null, string? sessionId = null, string? clientIP = null, string? protocol = null)
        {
            Log(LogLevel.Critical, message, module, exception, sessionId, clientIP, protocol);
        }

        /// <summary>
        /// 处理日志队列
        /// </summary>
        private async Task ProcessLogQueue()
        {
            while (_isRunning && !_cancellationTokenSource!.Token.IsCancellationRequested)
            {
                try
                {
                    if (_logQueue.TryDequeue(out var entry))
                    {
                        // 控制台输出
                        if (EnableConsoleOutput)
                        {
                            await WriteToConsole(entry);
                        }

                        // 文件输出
                        if (EnableFileOutput)
                        {
                            await WriteToFile(entry);
                        }
                    }
                    else
                    {
                        // 没有日志时短暂休眠
                        await Task.Delay(10, _cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // 日志系统本身的错误，直接输出到控制台
                    Console.WriteLine($"[CRITICAL] 日志系统错误: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 写入控制台
        /// </summary>
        private async Task WriteToConsole(LogEntry entry)
        {
            var formattedMessage = LogFormatter.FormatForConsole(entry, ShowTimestamp, ShowModule, ShowSession);
            
            if (EnableColorOutput)
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = entry.Level.GetColor();
                Console.WriteLine(formattedMessage);
                Console.ForegroundColor = originalColor;
            }
            else
            {
                Console.WriteLine(formattedMessage);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 写入文件
        /// </summary>
        private async Task WriteToFile(LogEntry entry)
        {
            try
            {
                var formattedMessage = LogFormatter.FormatForFile(entry);
                await File.AppendAllTextAsync(LogFilePath, formattedMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // 文件写入失败，输出到控制台
                Console.WriteLine($"[CRITICAL] 无法写入日志文件: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置日志级别
        /// </summary>
        public void SetLogLevel(LogLevel level)
        {
            CurrentLogLevel = level;
        }

        /// <summary>
        /// 设置日志文件路径
        /// </summary>
        public void SetLogFilePath(string filePath)
        {
            LogFilePath = filePath;
            
            // 确保目录存在
            var logDir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
        }

        /// <summary>
        /// 清空日志队列
        /// </summary>
        public void ClearQueue()
        {
            while (_logQueue.TryDequeue(out _)) { }
        }

        /// <summary>
        /// 获取队列中的日志数量
        /// </summary>
        public int GetQueueCount()
        {
            return _logQueue.Count;
        }
    }
}
