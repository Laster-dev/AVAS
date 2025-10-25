using System;

namespace TeamServer.Services
{
    /// <summary>
    /// 日志条目
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// 日志时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel Level { get; set; }

        /// <summary>
        /// 日志消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 异常信息（可选）
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// 模块名称（可选）
        /// </summary>
        public string? Module { get; set; }

        /// <summary>
        /// 会话ID（可选）
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// 客户端IP（可选）
        /// </summary>
        public string? ClientIP { get; set; }

        /// <summary>
        /// 协议类型（可选）
        /// </summary>
        public string? Protocol { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public LogEntry()
        {
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">消息</param>
        /// <param name="module">模块</param>
        public LogEntry(LogLevel level, string message, string? module = null)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message;
            Module = module;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">消息</param>
        /// <param name="exception">异常</param>
        /// <param name="module">模块</param>
        public LogEntry(LogLevel level, string message, Exception? exception, string? module = null)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message;
            Exception = exception;
            Module = module;
        }

        /// <summary>
        /// 获取格式化的时间戳
        /// </summary>
        public string GetFormattedTimestamp()
        {
            return Timestamp.ToString("HH:mm:ss.fff");
        }

        /// <summary>
        /// 获取完整的日志信息
        /// </summary>
        public string GetFullMessage()
        {
            var result = Message;
            if (Exception != null)
            {
                result += $"\n异常详情: {Exception.Message}";
                if (!string.IsNullOrEmpty(Exception.StackTrace))
                {
                    result += $"\n堆栈跟踪: {Exception.StackTrace}";
                }
            }
            return result;
        }
    }
}
