using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace cc.dw.src
{
    /// <summary>
    /// 优化的差异检测器 - 使用智能算法减少计算量
    /// </summary>
    public class OptimizedDiffDetector : IDisposable
    {
        private readonly int q;
        private readonly int cs;
        private readonly int a;
        private int b;
        private int c;
        private int d;

        // 缓存上一帧的像素数据
        private byte[] e;
        private int f;
        private int g;

        // 性能统计
        private int h;
        private int i;
        private long j;

        // 时间缓冲机制 - 防止屏幕撕裂
        private int k;
        private int _lastTransmitTime;
        private readonly int _timeWindowMs = 50; // 50ms时间窗口，快速响应
        private readonly int _maxAccumulatedChanges = 8; // 最大累积变化数量，降低阈值

        public OptimizedDiffDetector(int blockSize = 32, int diffThreshold = 16, int minChangesToTransmit = 3)
        {
            q = blockSize;
            cs = diffThreshold;
            a = minChangesToTransmit;
            d = 16; // 初始60FPS，更流畅
            c = Environment.TickCount;
            j = Environment.TickCount;
        }

        /// <summary>
        /// 快速检测图像是否有显著变化（带时间缓冲）
        /// </summary>
        public bool HasSignificantChanges(Bitmap currentFrame, out int changedBlocks, out int totalBlocks)
        {
            changedBlocks = 0;
            totalBlocks = 0;

            if (currentFrame == null) return false;

            // 获取当前帧的像素数据
            var currentData = GetBitmapData(currentFrame);
            if (currentData == null) return false;

            totalBlocks = (currentFrame.Width / q + 1) * (currentFrame.Height / q + 1);

            // 第一帧总是需要传输
            if (e == null || f != currentFrame.Width || g != currentFrame.Height)
            {
                e = currentData;
                f = currentFrame.Width;
                g = currentFrame.Height;
                changedBlocks = totalBlocks;
                k = 0;
                _lastTransmitTime = Environment.TickCount;
                return true;
            }

            // 使用超高速差异检测
            changedBlocks = DetectBlockDifferences(currentData, currentFrame.Width, currentFrame.Height);

            // 更新性能统计
            h = totalBlocks;
            i = changedBlocks;

            // 简化判断逻辑 - 关键点检测
            int currentTime = Environment.TickCount;
            bool shouldTransmit = false;

            // 如果有任何一个关键点发生变化，就认为有显著变化
            if (changedBlocks > 0)
            {
                shouldTransmit = true;
                k = 0;
                _lastTransmitTime = currentTime;
            }
            else
            {
                // 没有关键点变化，检查时间窗口
                k++;

                if (currentTime - _lastTransmitTime > _timeWindowMs)
                {
                    // 时间窗口已过，发送一个心跳包
                    shouldTransmit = true;
                    k = 0;
                    _lastTransmitTime = currentTime;
                }
            }

            // 只有在需要传输时才更新缓存
            if (shouldTransmit)
            {
                e = currentData;
                f = currentFrame.Width;
                g = currentFrame.Height;
                return true;
            }
            else
            {
                // 不需要传输，释放当前数据
                return false;
            }
        }

        /// <summary>
        /// 检测块级差异（最简单版本 - 直接比较）
        /// </summary>
        private int DetectBlockDifferences(byte[] currentData, int width, int height)
        {
            // 最简单的方案：如果数据长度不同，必定有变化
            if (currentData.Length != e.Length)
            {
                return 1;
            }

            // 直接比较前1000个字节，如果有任何差异就认为有变化
            int compareLength = Math.Min(1000, Math.Min(currentData.Length, e.Length));

            for (int i = 0; i < compareLength; i++)
            {
                if (currentData[i] != e[i])
                {
                    return 1; // 有变化
                }
            }

            return 0; // 无变化
        }



        /// <summary>
        /// 获取位图的原始像素数据
        /// </summary>
        private byte[] GetBitmapData(Bitmap bmp)
        {
            try
            {
                var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                int bytes = Math.Abs(bmpData.Stride) * bmp.Height;
                var rgbValues = new byte[bytes];

                Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);
                bmp.UnlockBits(bmpData);

                return rgbValues;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取动态延迟时间（毫秒）
        /// </summary>
        public int GetDynamicDelay()
        {
            int currentTime = Environment.TickCount;
            int timeSinceLastFrame = currentTime - c;

            // 每1000ms检查一次性能并调整延迟
            if (currentTime - j > 1000)
            {
                AdjustDelayBasedOnPerformance();
                j = currentTime;
            }

            c = currentTime;
            return Math.Max(10, d); // 最少10ms延迟
        }

        /// <summary>
        /// 基于性能调整延迟
        /// </summary>
        private void AdjustDelayBasedOnPerformance()
        {
            if (h == 0) return;

            double changeRatio = (double)i / h;

            // 根据变化比例动态调整帧率 - 更激进的响应
            if (changeRatio < 0.005) // 极少变化，降低帧率
            {
                d = Math.Min(d + 8, 100); // 最高10FPS
            }
            else if (changeRatio < 0.02) // 很少变化
            {
                d = Math.Max(d - 2, 33); // 30FPS
            }
            else if (changeRatio < 0.08) // 中等变化
            {
                d = Math.Max(d - 8, 16); // 60FPS
            }
            else // 大量变化
            {
                d = Math.Max(d - 16, 8); // 120FPS
            }
        }

        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        public string GetPerformanceStats()
        {
            if (h == 0) return "No performance data";

            int fps = 1000 / Math.Max(1, d);
            int timeSinceLastTransmit = Environment.TickCount - _lastTransmitTime;

            string status = "Idle";
            if (i > 0)
                status = "Active";
            else if (k > 0)
                status = "Buffered";

            return $"FPS: {fps}, KeyPoints: {i}/9, Accum: {k}, Time: {timeSinceLastTransmit}ms, Status: {status}";
        }

        public void Dispose()
        {
            e = null;
        }
    }
}
