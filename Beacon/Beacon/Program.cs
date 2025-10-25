using Beacon.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Beacon
{
    internal class Program
    {
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();


        static void tcpstart(string ip, int prot)
        {
            var client = new TCPBeacon();
            // 尝试初始连接
            if (client.Connect(ip, prot))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 初始连接成功: {ip}:{prot}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] TCP初始连接失败，将开始重连: {ip}:{prot}");
            }
            // 启动重连循环
            client.StartReconnectLoop();
        }

        static void udpstart(string ip, int port)
        {
            var udpclient = new UDPBeacon();
            // 尝试初始连接
            if (udpclient.Start(ip, port))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] UDP 初始连接成功: {ip}:{port}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] UDP 初始连接失败");
            }
        }

        static void WebSocketstart(string ip, int port)
        {
            var wsclient = new WebSocketBeacon();
            // 尝试初始连接
            if (wsclient.Start($"ws://{ip}:{port}/ws"))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] WebSocket 初始连接成功: {ip}:{port}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] WebSocket 初始连接失败");
            }
        }
        
        static void icmpstart(string ip, int port)
        {
            var icmpclient = new ICMPBeacon();
            // 尝试初始连接
            if (icmpclient.Connect(ip, port))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ICMP 初始连接成功: {ip}:{port}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ICMP 初始连接失败，将开始重连: {ip}:{port}");
            }
            // 启动重连循环
            icmpclient.StartReconnectLoop();
        }

        static void httpstart(string ip, int port)
        {
            var httpclient = new HttpBeacon();
            // 尝试初始连接
            if (httpclient.Connect(ip, port))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] HTTP 初始连接成功: {ip}:{port}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] HTTP 初始连接失败");
            }
        }
        static void dhcpstart(string ip, int port)
        {
            var dhcpclient = new DHCPBeacon();
            // 尝试初始连接
            if (dhcpclient.Start(ip, port))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DHCP 初始连接成功: {ip}:{port}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DHCP 初始连接失败");
            }
        }
        static void smbstart(string ip, int port)
        {
            var smbclient = new SMBBeacon();
            // 尝试初始连接
            if (smbclient.Start(ip, port))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SMB 初始连接成功: {ip}:{port}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SMB 初始连接失败");
            }
        }
        static void Main(string[] args)
        {
            SetProcessDPIAware();
            var s = A.pa();
            
            Console.WriteLine("=== Beacon 客户端 ===");
            Console.WriteLine("请选择连接类型:");
            Console.WriteLine("1. TCP");
            Console.WriteLine("2. UDP");
            Console.WriteLine("3. WebSocket");
            Console.WriteLine("4. ICMP");
            Console.WriteLine("5. HTTP");
            Console.WriteLine("6. DHCP");
            Console.WriteLine("7. SMB");
            Console.Write("请输入选择 (1-7): ");
            
            string choice = Console.ReadLine();
            Console.Write("请输入服务器IP地址: ");
            string ip = Console.ReadLine();
            Console.Write("请输入端口号: ");
            string portStr = Console.ReadLine();
            
            if (!int.TryParse(portStr, out int port))
            {
                Console.WriteLine("端口号格式错误，使用默认端口 8888");
                port = 8888;
            }
            
            Console.WriteLine($"\n正在连接到 {ip}:{port}...");
            
            switch (choice)
            {
                case "1":
                    tcpstart(ip, port);
                    break;
                case "2":
                    udpstart(ip, port);
                    break;
                case "3":
                    WebSocketstart(ip, port);
                    break;
                case "4":
                    icmpstart(ip, port);
                    break;
                case "5":
                    httpstart(ip, port);
                    break;
                case "6":
                    dhcpstart(ip, port);
                    break;
                case "7":
                    smbstart(ip, port);
                    break;
                default:
                    Console.WriteLine("无效选择，使用默认TCP连接");
                    tcpstart(ip, port);
                    break;
            }
            
            while (true)
            {
                Thread.Sleep(1000);
            }
        }
    }
}