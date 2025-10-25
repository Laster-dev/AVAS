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
using TeamServer.Data;

namespace AVASClient.HandlePacket
{
    internal class HandleClientInfo
    {
        public static async Task HandleAsync(ClientMsgPack msg)
        {
            await Task.Run(() => {
                Handle(msg);
            });
        }

        private static void Handle(ClientMsgPack unpack_msgpack)
        {
            // 创建一个新的Beacon实例
            BeaconInfo Beacon = new()
            {
                Id = unpack_msgpack.ForcePathObject("BeaconID").GetAsString(),
                ListenerId = unpack_msgpack.ForcePathObject("ListenerId").GetAsString(),
                IPProt = unpack_msgpack.ForcePathObject("IPProt").GetAsString(),
                IPAddress = unpack_msgpack.ForcePathObject("IPAddress").GetAsString(),
                LastSeenTime = DateTime.UtcNow,
                ConnectedTime = DateTime.Parse(unpack_msgpack.ForcePathObject("ConnectedTime").GetAsString())
            };
            Beacon.HWID = unpack_msgpack.ForcePathObject("HWID").GetAsString();
            Beacon.User = unpack_msgpack.ForcePathObject("User").GetAsString();
            Beacon.OS = unpack_msgpack.ForcePathObject("OS").GetAsString();
            Beacon.Path = unpack_msgpack.ForcePathObject("Path").GetAsString();
            Beacon.Admin = unpack_msgpack.ForcePathObject("Admin").GetAsString();
            Beacon.Perfor_mance = unpack_msgpack.ForcePathObject("Perfor_mance").GetAsString();
            Beacon.Anti_virus = unpack_msgpack.ForcePathObject("Anti_virus").GetAsString();
            Beacon.Install_ed = unpack_msgpack.ForcePathObject("Install_ed").GetAsString();
            Beacon.Group = unpack_msgpack.ForcePathObject("Group").GetAsString();
            Beacon.Notes = unpack_msgpack.ForcePathObject("Notes").GetAsString();
            Beacon.tg = unpack_msgpack.ForcePathObject("tg").GetAsString();
            Beacon.wx = unpack_msgpack.ForcePathObject("wx").GetAsString();

            // 检查是否已存在，防止重复添加
            var existingBeacon = MainWindowModle.Beacons.FirstOrDefault(b => b.Id == Beacon.Id);
            if (existingBeacon == null)
            {
                // 在 UI 线程上更新属性，确保 PropertyChanged 事件在正确的线程上触发
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        // 将Beacon添加到全局列表
                        MainWindowModle.Beacons.Add(Beacon);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                });
               
            }
            else
            {
                // 更新已有实例的信息
                existingBeacon.HWID = Beacon.HWID;
                existingBeacon.User = Beacon.User;
                existingBeacon.OS = Beacon.OS;
                existingBeacon.Path = Beacon.Path;
                existingBeacon.Admin = Beacon.Admin;
                existingBeacon.Perfor_mance = Beacon.Perfor_mance;
                existingBeacon.Anti_virus = Beacon.Anti_virus;
                existingBeacon.Install_ed = Beacon.Install_ed;
                existingBeacon.Group = Beacon.Group;
                existingBeacon.Notes = Beacon.Notes;
                existingBeacon.tg = Beacon.tg;
                existingBeacon.wx = Beacon.wx;
            }
            LogService.Instance.Info($"[+] Beacon上线: {Beacon.IPProt} - {Beacon.User}@{Beacon.OS}", "HandleClientInfo");
        }
    }
}
