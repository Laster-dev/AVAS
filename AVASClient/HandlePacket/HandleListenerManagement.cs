using Avalonia.Threading;
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
    internal class HandleListenerManagement
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
                string action = unpack_msgpack.ForcePathObject("Action").GetAsString();
                
                switch (action)
                {
                    case "GET_ACTIVE_RESPONSE":
                        HandleGetActiveResponse(unpack_msgpack);
                        break;
                    case "GET_SUPPORTED_RESPONSE":
                        HandleGetSupportedResponse(unpack_msgpack);
                        break;
                    case "ADD_RESPONSE":
                        HandleAddResponse(unpack_msgpack);
                        break;
                    case "REMOVE_RESPONSE":
                        HandleRemoveResponse(unpack_msgpack);
                        break;
                    case "START_RESPONSE":
                        HandleStartResponse(unpack_msgpack);
                        break;
                    case "STOP_RESPONSE":
                        HandleStopResponse(unpack_msgpack);
                        break;
                    case "ERROR_RESPONSE":
                        HandleErrorResponse(unpack_msgpack);
                        break;
                    default:
                        LogService.Instance.Warning($"收到未知的监听器管理响应: {action}", "HandleListenerManagement");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"处理监听器管理消息时发生异常: {ex.Message}", "HandleListenerManagement", ex);
            }
        }

        private static void HandleGetActiveResponse(ClientMsgPack unpack_msgpack)
        {
            try
            {
                bool success = unpack_msgpack.ForcePathObject("Success").GetAsInteger() == 1;
                if (success)
                {
                    string data = unpack_msgpack.ForcePathObject("Data").GetAsString();
                    
                    // 解析Base64编码的监听器列表数据
                    try
                    {
                        byte[] decodedBytes = Convert.FromBase64String(data);
                        var listenersArray = new ClientMsgPack();
                        listenersArray.DecodeFromBytesUnsafe(decodedBytes);
                        
                        // 清空现有监听器列表
                        ListenerManager.ClearListeners();
                        
                        // 解析每个监听器
                        for (int i = 0; i < 100; i++) // 假设最多100个监听器
                        {
                            try
                            {
                                string listenerData = listenersArray.ForcePathObject(i.ToString()).GetAsString();
                                if (string.IsNullOrEmpty(listenerData))
                                    break;
                                
                                byte[] listenerBytes = Convert.FromBase64String(listenerData);
                                var listenerPack = new ClientMsgPack();
                                listenerPack.DecodeFromBytesUnsafe(listenerBytes);
                                
                                var listener = new ListenerInfo
                                {
                                    Type = listenerPack.ForcePathObject("Type").GetAsString(),
                                    BindAddress = listenerPack.ForcePathObject("BindAddress").GetAsString(),
                                    Port = (int)listenerPack.ForcePathObject("Port").GetAsInteger(),
                                    IsRunning = listenerPack.ForcePathObject("IsRunning").GetAsInteger() == 1
                                };
                                
                                ListenerManager.AddListener(listener);
                            }
                            catch
                            {
                                // 如果解析失败，说明没有更多监听器了
                                break;
                            }
                        }
                        
                        LogService.Instance.Info($"已更新监听器列表，共 {ListenerManager.Listeners.Count} 个监听器", "HandleListenerManagement");
                        ListenerManager.UpdateStatus($"已更新监听器列表，共 {ListenerManager.Listeners.Count} 个监听器");
                    }
                    catch (Exception parseEx)
                    {
                        LogService.Instance.Error($"解析监听器列表数据失败: {parseEx.Message}", "HandleListenerManagement", parseEx);
                        ListenerManager.UpdateStatus($"解析监听器列表数据失败: {parseEx.Message}");
                    }
                }
                else
                {
                    LogService.Instance.Warning("获取活跃监听器列表失败", "HandleListenerManagement");
                    ListenerManager.UpdateStatus("获取活跃监听器列表失败");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"处理获取活跃监听器响应失败: {ex.Message}", "HandleListenerManagement", ex);
                ListenerManager.UpdateStatus($"处理获取活跃监听器响应失败: {ex.Message}");
            }
        }

        private static void HandleGetSupportedResponse(ClientMsgPack unpack_msgpack)
        {
            try
            {
                bool success = unpack_msgpack.ForcePathObject("Success").GetAsInteger() == 1;
                if (success)
                {
                    string data = unpack_msgpack.ForcePathObject("Data").GetAsString();
                    LogService.Instance.Info($"支持的监听器类型: {data}", "HandleListenerManagement");
                }
                else
                {
                    LogService.Instance.Warning("获取支持的监听器类型失败", "HandleListenerManagement");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"处理获取支持监听器响应失败: {ex.Message}", "HandleListenerManagement", ex);
            }
        }

        private static void HandleAddResponse(ClientMsgPack unpack_msgpack)
        {
            try
            {
                bool success = unpack_msgpack.ForcePathObject("Success").GetAsInteger() == 1;
                string message = unpack_msgpack.ForcePathObject("Message").GetAsString();
                
                if (success)
                {
                    LogService.Instance.Info($"监听器添加成功: {message}", "HandleListenerManagement");
                    ListenerManager.UpdateStatus($"监听器添加成功: {message}");
                }
                else
                {
                    LogService.Instance.Warning($"监听器添加失败: {message}", "HandleListenerManagement");
                    ListenerManager.UpdateStatus($"监听器添加失败: {message}");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"处理添加监听器响应失败: {ex.Message}", "HandleListenerManagement", ex);
                ListenerManager.UpdateStatus($"处理添加监听器响应失败: {ex.Message}");
            }
        }

        private static void HandleRemoveResponse(ClientMsgPack unpack_msgpack)
        {
            try
            {
                bool success = unpack_msgpack.ForcePathObject("Success").GetAsInteger() == 1;
                string message = unpack_msgpack.ForcePathObject("Message").GetAsString();
                
                if (success)
                {
                    LogService.Instance.Info($"监听器删除成功: {message}", "HandleListenerManagement");
                }
                else
                {
                    LogService.Instance.Warning($"监听器删除失败: {message}", "HandleListenerManagement");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"处理删除监听器响应失败: {ex.Message}", "HandleListenerManagement", ex);
            }
        }

        private static void HandleStartResponse(ClientMsgPack unpack_msgpack)
        {
            try
            {
                bool success = unpack_msgpack.ForcePathObject("Success").GetAsInteger() == 1;
                string message = unpack_msgpack.ForcePathObject("Message").GetAsString();
                
                if (success)
                {
                    LogService.Instance.Info($"监听器启动成功: {message}", "HandleListenerManagement");
                    ListenerManager.UpdateStatus($"监听器启动成功: {message}");
                }
                else
                {
                    LogService.Instance.Warning($"监听器启动失败: {message}", "HandleListenerManagement");
                    ListenerManager.UpdateStatus($"监听器启动失败: {message}");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"处理启动监听器响应失败: {ex.Message}", "HandleListenerManagement", ex);
                ListenerManager.UpdateStatus($"处理启动监听器响应失败: {ex.Message}");
            }
        }

        private static void HandleStopResponse(ClientMsgPack unpack_msgpack)
        {
            try
            {
                bool success = unpack_msgpack.ForcePathObject("Success").GetAsInteger() == 1;
                string message = unpack_msgpack.ForcePathObject("Message").GetAsString();
                
                if (success)
                {
                    LogService.Instance.Info($"监听器停止成功: {message}", "HandleListenerManagement");
                    ListenerManager.UpdateStatus($"监听器停止成功: {message}");
                }
                else
                {
                    LogService.Instance.Warning($"监听器停止失败: {message}", "HandleListenerManagement");
                    ListenerManager.UpdateStatus($"监听器停止失败: {message}");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"处理停止监听器响应失败: {ex.Message}", "HandleListenerManagement", ex);
                ListenerManager.UpdateStatus($"处理停止监听器响应失败: {ex.Message}");
            }
        }

        private static void HandleErrorResponse(ClientMsgPack unpack_msgpack)
        {
            try
            {
                string message = unpack_msgpack.ForcePathObject("Message").GetAsString();
                LogService.Instance.Error($"监听器管理错误: {message}", "HandleListenerManagement");
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"处理监听器管理错误响应失败: {ex.Message}", "HandleListenerManagement", ex);
            }
        }
    }
}
