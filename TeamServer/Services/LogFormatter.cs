using System;
using System.Text;

namespace TeamServer.Services
{
    /// <summary>
    /// 日志格式化器
    /// </summary>
    public static class LogFormatter
    {
        /// <summary>
        /// 格式化日志条目为控制台输出
        /// </summary>
        /// <param name="entry">日志条目</param>
        /// <param name="includeTimestamp">是否包含时间戳</param>
        /// <param name="includeModule">是否包含模块名</param>
        /// <param name="includeSession">是否包含会话信息</param>
        /// <returns>格式化后的字符串</returns>
        public static string FormatForConsole(LogEntry entry, bool includeTimestamp = true, bool includeModule = true, bool includeSession = true)
        {
            var sb = new StringBuilder();

            // 时间戳
            if (includeTimestamp)
            {
                sb.Append($"[{entry.GetFormattedTimestamp()}] ");
            }

            // 日志级别符号
            sb.Append($"{entry.Level.GetSymbol()} ");

            // 模块名
            if (includeModule && !string.IsNullOrEmpty(entry.Module))
            {
                sb.Append($"[{entry.Module}] ");
            }

            // 协议类型
            if (!string.IsNullOrEmpty(entry.Protocol))
            {
                sb.Append($"[{entry.Protocol}] ");
            }

            // 会话信息
            if (includeSession && !string.IsNullOrEmpty(entry.SessionId))
            {
                sb.Append($"[{entry.SessionId}] ");
            }

            // 客户端IP
            if (!string.IsNullOrEmpty(entry.ClientIP))
            {
                sb.Append($"[{entry.ClientIP}] ");
            }

            // 消息内容
            sb.Append(entry.Message);

            return sb.ToString();
        }

        /// <summary>
        /// 格式化日志条目为文件输出
        /// </summary>
        /// <param name="entry">日志条目</param>
        /// <returns>格式化后的字符串</returns>
        public static string FormatForFile(LogEntry entry)
        {
            var sb = new StringBuilder();

            // 完整时间戳
            sb.Append($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] ");

            // 日志级别
            sb.Append($"[{entry.Level.GetDisplayName()}] ");

            // 模块名
            if (!string.IsNullOrEmpty(entry.Module))
            {
                sb.Append($"[{entry.Module}] ");
            }

            // 协议类型
            if (!string.IsNullOrEmpty(entry.Protocol))
            {
                sb.Append($"[{entry.Protocol}] ");
            }

            // 会话信息
            if (!string.IsNullOrEmpty(entry.SessionId))
            {
                sb.Append($"[{entry.SessionId}] ");
            }

            // 客户端IP
            if (!string.IsNullOrEmpty(entry.ClientIP))
            {
                sb.Append($"[{entry.ClientIP}] ");
            }

            // 消息内容
            sb.Append(entry.Message);

            // 异常信息
            if (entry.Exception != null)
            {
                sb.Append($"\n异常: {entry.Exception.Message}");
                if (!string.IsNullOrEmpty(entry.Exception.StackTrace))
                {
                    sb.Append($"\n堆栈: {entry.Exception.StackTrace}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 格式化简化的控制台输出（用于高频日志）
        /// </summary>
        /// <param name="entry">日志条目</param>
        /// <returns>格式化后的字符串</returns>
        public static string FormatSimple(LogEntry entry)
        {
            var sb = new StringBuilder();

            // 时间戳（简化）
            sb.Append($"[{entry.Timestamp:HH:mm:ss}] ");

            // 日志级别符号
            sb.Append($"{entry.Level.GetSymbol()} ");

            // 模块名（简化）
            if (!string.IsNullOrEmpty(entry.Module))
            {
                sb.Append($"[{entry.Module}] ");
            }

            // 消息内容
            sb.Append(entry.Message);

            return sb.ToString();
        }
    }
}
