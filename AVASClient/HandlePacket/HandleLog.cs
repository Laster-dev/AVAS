using Avalonia.Controls.Notifications;
using AVASClient.MessagePackLib;
using AVASClient.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AVASClient.HandlePacket
{
    internal class HandleLog
    {
        public static async Task HandleAsync(ClientMsgPack msg)
        {
            await Task.Run(() => {
                Handle(msg);
            });
        }
        private static void Handle(ClientMsgPack unpack_msgpack)
        {
            try
            {
                string logMessage = unpack_msgpack.ForcePathObject("Message").GetAsString();
                //Settings._manager?.Show(new Notification("提示", logMessage, NotificationType.Information));
                LogService.Instance.Info(logMessage, "HandleLog");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling log message: {ex.Message}");
            }
        }
    }
}
