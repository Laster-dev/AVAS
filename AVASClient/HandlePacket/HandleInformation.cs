using AVASClient.MessagePackLib;
using AVASClient.Models;
using AVASClient.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AVASClient.HandlePacket
{
    internal class HandleInformation
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
                string Information = unpack_msgpack.ForcePathObject("Information").GetAsString();
                //将Information写出到文本文件，文件名路径".\ClientFolder\{HWID}\Information\{time}.txt"，然后调用系统默认工具打开txt文件
                //HWID从BeaconInfo用BID获取
                string BID = unpack_msgpack.ForcePathObject("BID").GetAsString();
                var beacon = MainWindowModle.Beacons.FirstOrDefault(b => b.Id == BID);
                if (beacon == null)
                {
                    LogService.Instance.Error($"未找到BID为{BID}的Beacon，无法保存Information", "HandleInformation");
                    return;
                }
                string HWID = beacon.HWID;
                string folderPath = System.IO.Path.Combine(AppContext.BaseDirectory, "ClientFolder", HWID, "Information");
                if (!System.IO.Directory.Exists(folderPath))
                {
                    System.IO.Directory.CreateDirectory(folderPath);
                }
                string filePath = System.IO.Path.Combine(folderPath, $"{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.txt");
                System.IO.File.WriteAllText(filePath, Information, Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = filePath,
                    UseShellExecute = true,
                    Verb = "open"
                });
                string logMessage = $"已保存并打开BID为{BID}的Beacon的Information，文件路径：{filePath}";



                //Settings._manager?.Show(new Notification("提示", logMessage, NotificationType.Information));
                //LogService.Instance.Info(logMessage, "HandleInformation");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling log message: {ex.Message}");
            }
        }
    }
}
