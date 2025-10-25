using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AVASClient.Models
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    public class LogEntry : INotifyPropertyChanged
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

        private DateTime _timestamp;
        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }

        private LogLevel _level;
        public LogLevel Level
        {
            get => _level;
            set => SetProperty(ref _level, value);
        }

        private string _message = string.Empty;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        private string _source = string.Empty;
        public string Source
        {
            get => _source;
            set => SetProperty(ref _source, value);
        }

        private string _threadId = string.Empty;
        public string ThreadId
        {
            get => _threadId;
            set => SetProperty(ref _threadId, value);
        }

        private Exception? _exception;
        public Exception? Exception
        {
            get => _exception;
            set => SetProperty(ref _exception, value);
        }

        public LogEntry()
        {
            Timestamp = DateTime.Now;
            ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId.ToString();
        }

        public LogEntry(LogLevel level, string message, string source = "", Exception? exception = null) : this()
        {
            Level = level;
            Message = message;
            Source = source;
            Exception = exception;
        }

        public override string ToString()
        {
            var exceptionInfo = Exception != null ? $" | Exception: {Exception.Message}" : "";
            return $"[{Timestamp:HH:mm:ss.fff}] [{Level}] [{Source}] {Message}{exceptionInfo}";
        }

        public string ToFormattedString()
        {
            var exceptionInfo = Exception != null ? $"\nException: {Exception}" : "";
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] [{Source}] {Message}{exceptionInfo}";
        }
    }
}
