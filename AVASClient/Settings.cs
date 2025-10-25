using Avalonia.Controls.Notifications;
using AVASClient.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AVASClient
{
    public static class Settings
    {
        public static WindowNotificationManager? _manager;  // 通知管理器

        public static LogViewModel? _logViewModel;         // 日志视图模型

        public static Client? client { get; set; }          // 客户端实例
        public static string? ServerAddress { get; set; }   // 服务器地址
        public static int ServerPort { get; set; }          // 服务器端口
        //public static string? CID { get; set; }                  // 客户端ID,用来区分不同客户端
    }
}
