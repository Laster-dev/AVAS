using Avalonia.Threading;
using AVASClient.MessagePackLib;
using AVASClient.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeamServer.Data;

namespace AVASClient.HandlePacket
{
    internal class HandleBeaconList
    {
        public static async Task HandleAsync(ClientMsgPack msg)
        {
            await Task.Run(() => {
                Handle(msg);
            });
        }
        private static void Handle(ClientMsgPack unpack_msgpack)
        {
            var info = BeaconList.Deserialize(unpack_msgpack.ForcePathObject("Beacons").AsString);

            // 在UI线程上更新集合
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // 清空现有数据，避免重复
                MainWindowModle.Beacons.Clear();

                foreach (var b in info)
                {
                    MainWindowModle.Beacons.Add(b);
                }
            });
        }
    }
}
