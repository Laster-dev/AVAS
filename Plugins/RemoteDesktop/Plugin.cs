using Beacon.MessagePackLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Timers;

namespace cc
{
    public class Plugin
    {
        /*
         1. 插件必须是公有类，类名必须是Plugin
         2. 插件必须有一个公有的构造函数，接收发送数据的方法和销毁实例的方法
         3. 插件必须有一个公有的实例方法Read，返回值为void，参数是收到的数据包
         4. 插件必须有一个公有的静态字段DLLINFO，用于标识插件功能
         5. 插件必须有一个公有的静态方法SendFramed，用于发送数据
         6. 插件必须有一个公有的静态方法Destroy，用于销毁实例
         */
        public static string DLLINFO = "RemoteDesktop";
        
        private static Action<byte[]> _sendFramed;    //发送数据的方法
        private static Action _destroyInstance;       //销毁实例的方法
        //创建一个计时器，如果说五秒内没有收到心跳包数据，则销毁实例
        private static System.Timers.Timer _heartbeatTimer;        //心跳包计时器
        private static DateTime _lastHeartbeatTime;                //上次收到心跳包的时间
        private static int _heartbeatInterval = 5000;              //心跳包间隔时间

        public Plugin(Action<byte[]> sendFramed, Action destroyInstance)
        {
            _sendFramed = sendFramed ?? throw new ArgumentNullException(nameof(sendFramed));
            _destroyInstance = destroyInstance ?? throw new ArgumentNullException(nameof(destroyInstance));

            // 初始化并启动心跳检测
            _lastHeartbeatTime = DateTime.UtcNow;
            _heartbeatTimer = new System.Timers.Timer(1000);
            _heartbeatTimer.AutoReset = true;
            _heartbeatTimer.Elapsed += HeartbeatTimerOnElapsed;
            _heartbeatTimer.Start();
        }

        public void Read(byte[] beaconMsgPackbyte)
        {
            new Thread(() =>
            {
                try
                {
                    BeaconMsgPack _beaconMsgPack = new BeaconMsgPack();
                    _beaconMsgPack.DecodeFromBytesUnsafe(beaconMsgPackbyte);
                    Packet.Read(_beaconMsgPack);
                }
                catch (Exception ex)
                {
                    // 处理异常，可以选择是否销毁实例
                    Console.WriteLine($"Plugin execution error: {ex.Message}");
                }
            }).Start();
        }

        //发送数据的方法
        public static void SendFramed(byte[] data)
        {
            _sendFramed?.Invoke(data);
        }

        //销毁实例的方法
        public static void Destroy()
        {
            // 停止并释放心跳定时器
            try
            {
                if (_heartbeatTimer != null)
                {
                    _heartbeatTimer.Stop();
                    _heartbeatTimer.Elapsed -= HeartbeatTimerOnElapsed;
                    _heartbeatTimer.Dispose();
                    _heartbeatTimer = null;
                }
            }
            catch { }

            _destroyInstance?.Invoke();
        }

        // 由 Packet 在收到心跳包时调用，刷新心跳时间
        public static void RefreshHeartbeat()
        {
            _lastHeartbeatTime = DateTime.UtcNow;
            GC.Collect();
        }

        private static void HeartbeatTimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                var elapsedMs = (DateTime.UtcNow - _lastHeartbeatTime).TotalMilliseconds;
                if (elapsedMs > _heartbeatInterval)
                {
                    // 超时未收到心跳，销毁实例
                    Destroy();
                }
            }
            catch
            {
                // 忽略心跳线程中的异常
            }
        }
    }
}
