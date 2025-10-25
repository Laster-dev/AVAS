using AVASClient.Helper;
using AVASClient.MessagePackLib;
using AVASClient.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AVASClient.HandlePacket
{
    internal class HandleRemoteDesktop
    {
        private const string DLLINFO = "RemoteDesktop";


        public static async Task HandleAsync(ClientMsgPack msg)
        {
            await Task.Run(() => {
                Handle(msg);
            });
        }

        private static void SendStop(string BID)
        {
            using (ClientMsgPack clientMsgPack = new ClientMsgPack())
            {
                // 先设置包的类型，以便服务端分包
                clientMsgPack.ForcePathObject("DLLINFO").SetAsString(DLLINFO);
                // 再设置CID，以便服务端找到对应的beacon
                clientMsgPack.ForcePathObject("BID").SetAsString(BID);
                // 最后设置包的具体内容
                clientMsgPack.ForcePathObject("Pac_ket").AsString = "Stop";
                Settings.client?.SendFramed(clientMsgPack.Encode2Bytes());
            }
        }
        private static async Task Handle(ClientMsgPack unpack_msgpack)
        {
            try
            {
                string BID = unpack_msgpack.ForcePathObject("BID").AsString;
                string windowName = "RemoteDesktop:" + BID;
                // 用 as 防止类型异常
                var window = await Helper.FindForm.FindWindowByNameSafe(windowName) as Form_RemoteDesktop;

                // 判断窗口有效性
                if (window == null || !window.ISOK)
                {
                    SendStop(BID);
                    return;
                }

                // 获取数据流
                byte[] rdpStream = unpack_msgpack.ForcePathObject("Stream").GetAsBytes();
                if (rdpStream == null || rdpStream.Length == 0)
                {
                    LogService.Instance.Warning("RdpStream 为空", "HandleRemoteDesktop");
                    return;
                }

                // 处理远程桌面数据
                ProcessImageDataSync(window, rdpStream);
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("处理 RemoteDesktop 数据包时发生异常", "HandleRemoteDesktop", ex);
            }
            finally
            {
                // 统一释放资源，防止遗漏
                unpack_msgpack?.Dispose();
            }
        }
        /// <summary>
        /// 同步处理图像数据，支持数据重组
        /// </summary>
        private static void ProcessImageDataSync(Form_RemoteDesktop RD, byte[] rdpStream)
        {
            if (RD == null || rdpStream == null || rdpStream.Length == 0)
            {
                Debug.WriteLine("参数无效或数据流为空，跳过处理");
                return;
            }

            // 若 RD.decoder 非线程安全，建议开启锁
            // lock (RD.syncPicbox)
            // {
            if (RD.decoder == null)
            {
                Debug.WriteLine("解码器未初始化，跳过处理");
                return;
            }

            Bitmap decodedImage = null;
            try
            {
                using (var stream = new MemoryStream(rdpStream))
                {
                    stream.Position = 0; // 从头读取
                    decodedImage = RD.decoder.DecodeData(stream);

                    if (decodedImage == null)
                    {
                        Debug.WriteLine("解码返回空图像");
                        return;
                    }
                    if (!IsImageValid(decodedImage))
                    {
                        Debug.WriteLine("解码图像无效，将释放资源");
                        decodedImage.Dispose();
                        return;
                    }
                }

                // 交给UI线程处理，UpdateUIAsync内部会负责释放decodedImage
                _ = UpdateUIAsync(RD, decodedImage);
                decodedImage = null; // 避免finally重复释放
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"图像解码或处理异常: {ex}");
                decodedImage?.Dispose(); // 只在异常时补充释放
            }
            // }
        }
        public static Avalonia.Media.Imaging.Bitmap ConvertToAvaloniaBitmap(Image bitmap)
        {
            if (bitmap == null)
                return null;

            try
            {
                // 直接使用原始图像，避免不必要的复制
                if (bitmap is System.Drawing.Bitmap bmp)
                {
                    var bitmapdata = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {

                        var avaloniaBitmap = new Avalonia.Media.Imaging.Bitmap(
                            Avalonia.Platform.PixelFormat.Bgra8888, 
                            Avalonia.Platform.AlphaFormat.Premul,
                            bitmapdata.Scan0,
                            new Avalonia.PixelSize(bitmapdata.Width, bitmapdata.Height),
                            new Avalonia.Vector(96, 96),
                            bitmapdata.Stride);
                        return avaloniaBitmap;
                    }
                    finally
                    {
                        bmp.UnlockBits(bitmapdata);
                    }
                }
                //else
                //{
                //    // 如果不是Bitmap，则创建副本
                //    using (var bitmapTmp = new System.Drawing.Bitmap(bitmap))
                //    {
                //        var bitmapdata = bitmapTmp.LockBits(new Rectangle(0, 0, bitmapTmp.Width, bitmapTmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                //        try
                //        {
                //            var avaloniaBitmap = new Avalonia.Media.Imaging.Bitmap(
                //                Avalonia.Platform.PixelFormat.Bgra8888, 
                //                Avalonia.Platform.AlphaFormat.Premul,
                //                bitmapdata.Scan0,
                //                new Avalonia.PixelSize(bitmapdata.Width, bitmapdata.Height),
                //                new Avalonia.Vector(96, 96),
                //                bitmapdata.Stride);
                //            return avaloniaBitmap;
                //        }
                //        finally
                //        {
                //            bitmapTmp.UnlockBits(bitmapdata);
                //        }
                //    }
                //}
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"图像转换异常: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// 异步更新UI，优化性能
        /// </summary>
        private static async Task UpdateUIAsync(Form_RemoteDesktop RD, Bitmap newImage)
        {
            if (RD == null)
            {
                newImage?.Dispose();
                return;
            }

            if (RD.VideoImage == null)
            {
                Debug.WriteLine("图片框无效或已被释放");
                newImage?.Dispose();
                return;
            }

            Avalonia.Media.Imaging.Bitmap avaloniaBitmap = null;
            try
            {
                // 使用using管理newImage和avaloniaBitmap的生命周期
                using (newImage)
                {
                    avaloniaBitmap = ConvertToAvaloniaBitmap(newImage);
                }
                // 此时 newImage 已释放，avaloniaBitmap 还未

                if (avaloniaBitmap != null)
                {
                    try
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            // 释放旧的 Source
                            if (RD.VideoImage.Source is IDisposable disposable)
                            {
                                try { disposable.Dispose(); } catch { }
                            }
                            RD.VideoImage.Source = avaloniaBitmap;
                            // 注意：不能在这里释放avaloniaBitmap，控件还要用
                        });
                        // UI设置成功后，不再释放avaloniaBitmap
                        avaloniaBitmap = null; // 防止finally里释放
                    }
                    catch (Exception uiEx)
                    {
                        Debug.WriteLine($"UI更新异常: {uiEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"异步界面更新错误: {ex.Message}");
                Debug.WriteLine($"异常类型: {ex.GetType().Name}");
            }
            finally
            {
                // 只有没被赋给控件的bitmap需要释放
                avaloniaBitmap?.Dispose();
            }
        }

        /// <summary>
        /// 按照原始逻辑更新界面（保留作为备用）
        /// </summary>
        private async void UpdateUIOriginal(Form_RemoteDesktop RD, Bitmap newImage)
        {
            try
            {
                if (RD == null)
                {
                    if (newImage == null)
                        return;
                    newImage?.Dispose();
                    return;
                }

                // 检查 PictureBox 是否存在并且有效
                if (RD.VideoImage == null)
                {
                    Debug.WriteLine("图片框无效或已被释放");
                    newImage?.Dispose();
                    return;
                }

                // 切换到UI线程更新
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RD.VideoImage.Source = ConvertToAvaloniaBitmap(newImage);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"界面更新错误: {ex.Message}");
                Debug.WriteLine($"异常类型: {ex.GetType().Name}");
                newImage?.Dispose();
            }
        }
        /// <summary>
        /// 检查图像是否有效
        /// </summary>
        private static bool IsImageValid(Image image)
        {
            if (image == null) return false;

            try
            {
                // 尝试访问图像的基本属性来验证其有效性
                var width = image.Width;
                var height = image.Height;
                var format = image.RawFormat;

                // 检查尺寸是否合理
                if (width <= 0 || height <= 0)
                {
                    Debug.WriteLine($"图像尺寸无效: {width}x{height}");
                    return false;
                }

                // 检查尺寸是否过大（防止内存问题）
                if (width > 10000 || height > 10000)
                {
                    Debug.WriteLine($"图像尺寸过大: {width}x{height}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"验证图像有效性时发生错误: {ex.Message}");
                return false;
            }
        }

    }
}
