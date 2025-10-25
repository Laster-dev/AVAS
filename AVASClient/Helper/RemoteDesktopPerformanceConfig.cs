using System;
using System.Collections.Generic;

namespace AVASClient.Helper
{
    /// <summary>
    /// 远程桌面性能配置类
    /// </summary>
    public static class RemoteDesktopPerformanceConfig
    {
        // 性能配置参数
        public static int TargetFPS { get; set; } = 30;           // 目标帧率
        public static int MaxFPS { get; set; } = 60;              // 最大帧率
        public static int FrameSkipThreshold { get; set; } = 2;    // 帧跳过阈值
        public static int JpegQuality { get; set; } = 85;          // JPEG质量 (1-100)
        public static bool EnableFrameSkipping { get; set; } = true; // 是否启用帧跳过
        public static bool EnablePerformanceLogging { get; set; } = true; // 是否启用性能日志
        
        // 性能统计
        private static readonly Dictionary<string, PerformanceStats> _performanceStats = new Dictionary<string, PerformanceStats>();
        
        /// <summary>
        /// 性能统计信息
        /// </summary>
        public class PerformanceStats
        {
            public int TotalFrames { get; set; }
            public int SkippedFrames { get; set; }
            public DateTime LastUpdateTime { get; set; }
            public double AverageFPS { get; set; }
            public double CurrentFPS { get; set; }
        }
        
        /// <summary>
        /// 更新性能统计
        /// </summary>
        public static void UpdateStats(string windowId, bool frameSkipped)
        {
            if (!EnablePerformanceLogging) return;
            
            if (!_performanceStats.ContainsKey(windowId))
            {
                _performanceStats[windowId] = new PerformanceStats
                {
                    LastUpdateTime = DateTime.Now
                };
            }
            
            var stats = _performanceStats[windowId];
            var now = DateTime.Now;
            var timeDiff = (now - stats.LastUpdateTime).TotalSeconds;
            
            if (frameSkipped)
            {
                stats.SkippedFrames++;
            }
            else
            {
                stats.TotalFrames++;
                
                if (timeDiff > 0)
                {
                    stats.CurrentFPS = 1.0 / timeDiff;
                    stats.AverageFPS = stats.TotalFrames / (now - _performanceStats[windowId].LastUpdateTime).TotalSeconds;
                }
                
                stats.LastUpdateTime = now;
            }
        }
        
        /// <summary>
        /// 获取性能统计
        /// </summary>
        public static PerformanceStats GetStats(string windowId)
        {
            return _performanceStats.ContainsKey(windowId) ? _performanceStats[windowId] : null;
        }
        
        /// <summary>
        /// 清理性能统计
        /// </summary>
        public static void ClearStats(string windowId)
        {
            _performanceStats.Remove(windowId);
        }
        
        /// <summary>
        /// 清理所有性能统计
        /// </summary>
        public static void ClearAllStats()
        {
            _performanceStats.Clear();
        }
        
        /// <summary>
        /// 获取所有窗口的性能统计
        /// </summary>
        public static Dictionary<string, PerformanceStats> GetAllStats()
        {
            return new Dictionary<string, PerformanceStats>(_performanceStats);
        }
        
        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public static void ResetToDefaults()
        {
            TargetFPS = 30;
            MaxFPS = 60;
            FrameSkipThreshold = 2;
            JpegQuality = 85;
            EnableFrameSkipping = true;
            EnablePerformanceLogging = true;
        }
    }
}
