using AVASClient.Models;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace AVASClient.Converters
{
    public class LogLevelToColorConverter : IValueConverter
    {
        public static readonly LogLevelToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Debug => Brushes.Gray,
                    LogLevel.Info => Brushes.Black,
                    LogLevel.Warning => Brushes.Orange,
                    LogLevel.Error => Brushes.Red,
                    LogLevel.Fatal => Brushes.DarkRed,
                    _ => Brushes.Black
                };
            }
            return Brushes.Black;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
