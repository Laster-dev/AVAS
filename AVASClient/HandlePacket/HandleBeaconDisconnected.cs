using Avalonia.Threading;
using AVASClient.MessagePackLib;
using AVASClient.Models;
using AVASClient.Services;
using DataList.Beacon.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AVASClient.HandlePacket
{
    internal class HandleBeaconDisconnected
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
                string beaconId = unpack_msgpack.ForcePathObject("BeaconID").GetAsString();
                string ipProt = unpack_msgpack.ForcePathObject("IPProt").GetAsString();

                
                // 在 UI 线程上更新集合
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        // 查找并移除对应的 Beacon
                        var beaconToRemove = MainWindowModle.Beacons.FirstOrDefault(b => b.Id == beaconId);
                        if (beaconToRemove != null)
                        {
                            MainWindowModle.Beacons.Remove(beaconToRemove);
                            LogService.Instance.Info($"Beacon 下线: {beaconId} - {ipProt}", "HandleBeaconDisconnected");
                        }
                      
                    }
                    catch (Exception ex)
                    {
                        LogService.Instance.Error($"处理 Beacon 断开连接时发生异常", "HandleBeaconDisconnected", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("处理 Beacon 断开连接通知时发生异常", "HandleBeaconDisconnected", ex);
            }
        }
    }
}
