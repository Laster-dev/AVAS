using System;

namespace TeamServer.Services
{
    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 调试信息 - 详细的调试信息，通常只在诊断问题时使用
        /// </summary>
        Debug = 0,
        
        /// <summary>
        /// 信息 - 一般信息，记录程序运行状态
        /// </summary>
        Info = 1,
        
        /// <summary>
        /// 成功 - 操作成功完成
        /// </summary>
        Success = 2,
        
        /// <summary>
        /// 警告 - 潜在的问题，程序可以继续运行
        /// </summary>
        Warning = 3,
        
        /// <summary>
        /// 错误 - 错误事件，但程序可以继续运行
        /// </summary>
        Error = 4,
        
        /// <summary>
        /// 严重错误 - 严重错误事件，程序可能无法继续运行
        /// </summary>
        Critical = 5
    }

    /// <summary>
    /// 日志级别扩展方法
    /// </summary>
    public static class LogLevelExtensions
    {
        /// <summary>
        /// 获取日志级别的显示名称
        /// </summary>
        public static string GetDisplayName(this LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => "DEBUG",
                LogLevel.Info => "INFO ",
                LogLevel.Success => "SUCCESS",
                LogLevel.Warning => "WARN ",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "CRITICAL",
                _ => "UNKNOWN"
            };
        }

        /// <summary>
        /// 获取日志级别的颜色代码
        /// </summary>
        public static ConsoleColor GetColor(this LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Success => ConsoleColor.Green,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };
        }

        /// <summary>
        /// 获取日志级别的符号
        /// </summary>
        public static string GetSymbol(this LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => "[?]",
                LogLevel.Info => "[i]",
                LogLevel.Success => "[+]",
                LogLevel.Warning => "[!]",
                LogLevel.Error => "[-]",
                LogLevel.Critical => "[X]",
                _ => "[?]"
            };
        }
    }
}
