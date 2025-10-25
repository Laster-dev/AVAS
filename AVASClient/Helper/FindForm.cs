using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AVASClient.Helper
{
    internal class FindForm
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task<Window?> FindWindowByNameSafe(string windowName)
     => Dispatcher.UIThread.CheckAccess()
         ? FindWindowByName(windowName)
         : await Dispatcher.UIThread.InvokeAsync(() => FindWindowByName(windowName));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Window? FindWindowByName(string windowName)
        {
            var app = Application.Current;
            if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.Windows.FirstOrDefault(w => w.Tag?.ToString() == windowName);
            return null;
        }
    }
}
