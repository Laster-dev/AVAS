using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace FileManager.Helper
{
    public static class UserDesktopHelper
    {
        /// <summary>
        /// 获取当前登录的第一个活动用户的桌面路径（不做任何兜底）。
        /// </summary>
        /// <returns>返回桌面路径，找不到则返回 null。</returns>
        public static string GetInteractiveUserDesktopPath()
        {
            try
            {
                // 通过 WTS 获取第一个活动登录用户
                string sid = GetFirstActiveUserSid();
                if (!string.IsNullOrWhiteSpace(sid))
                {
                    string profilePath = GetUserProfilePath(sid);
                    if (!string.IsNullOrWhiteSpace(profilePath))
                    {
                        string desktop = Path.Combine(profilePath, "Desktop");
                        if (Directory.Exists(desktop))
                        {
                            return desktop;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetUserProfilePath(string sid)
        {
            if (string.IsNullOrWhiteSpace(sid)) return null;
            try
            {
                string keyPath = $"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList\\{sid}";
                using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        var path = key.GetValue("ProfileImagePath") as string;
                        return path;
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetFirstActiveUserSid()
        {
            IntPtr ppSessionInfo = IntPtr.Zero;
            int count = 0;
            try
            {
                if (!WTSEnumerateSessions(IntPtr.Zero, 0, 1, out ppSessionInfo, out count))
                {
                    return null;
                }

                int dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                for (int i = 0; i < count; i++)
                {
                    IntPtr current = new IntPtr(ppSessionInfo.ToInt64() + i * dataSize);
                    var si = (WTS_SESSION_INFO)Marshal.PtrToStructure(current, typeof(WTS_SESSION_INFO));
                    if (si.State == WTS_CONNECTSTATE_CLASS.WTSActive)
                    {
                        string username = WTSQueryString(si.SessionId, WTS_INFO_CLASS.WTSUserName);
                        string domain = WTSQueryString(si.SessionId, WTS_INFO_CLASS.WTSDomainName);
                        if (!string.IsNullOrWhiteSpace(username))
                        {
                            try
                            {
                                var account = string.IsNullOrWhiteSpace(domain) ? new NTAccount(username) : new NTAccount(domain, username);
                                var sidObj = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
                                return sidObj.Value;
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (ppSessionInfo != IntPtr.Zero)
                {
                    WTSFreeMemory(ppSessionInfo);
                }
            }
            return null;
        }

        private static string WTSQueryString(int sessionId, WTS_INFO_CLASS infoClass)
        {
            IntPtr buffer;
            int bytesReturned;
            if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, infoClass, out buffer, out bytesReturned) && buffer != IntPtr.Zero)
            {
                try
                {
                    string s = Marshal.PtrToStringUni(buffer);
                    return s;
                }
                finally
                {
                    WTSFreeMemory(buffer);
                }
            }
            return null;
        }

        // WTS API 定义
        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSEnumerateSessions(IntPtr hServer, int Reserved, int Version, out IntPtr ppSessionInfo, out int pCount);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool WTSQuerySessionInformation(IntPtr hServer, int sessionId, WTS_INFO_CLASS wtsInfoClass, out IntPtr ppBuffer, out int pBytesReturned);

        private enum WTS_INFO_CLASS
        {
            WTSInitialProgram = 0,
            WTSApplicationName = 1,
            WTSWorkingDirectory = 2,
            WTSOEMId = 3,
            WTSSessionId = 4,
            WTSUserName = 5,
            WTSWinStationName = 6,
            WTSDomainName = 7,
            WTSConnectState = 8,
            WTSClientBuildNumber = 9,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            public int SessionId;
            public IntPtr pWinStationName;
            public WTS_CONNECTSTATE_CLASS State;
        }

        private enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }
    }
}
