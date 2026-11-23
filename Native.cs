using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace SnowIsland;

static class Native
{
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct PBI { public IntPtr Exit, Peb, Aff, Pri, Pid, Parent; }
    [StructLayout(LayoutKind.Sequential)] struct SPS { public byte AC, Flag, Pct, Saver; public int Life, Full; }
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] public static extern short GetKeyState(int vKey);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr h, int c, ref PBI i, int size, out int ret);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(int acc, bool inh, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out SPS s);

    public static Point Cursor(Visual v)
    {
        GetCursorPos(out var p);
        var d = VisualTreeHelper.GetDpi(v);
        return new Point(p.X / d.DpiScaleX, p.Y / d.DpiScaleY);
    }

    public static void ClickThrough(Window w)
    {
        var h = new WindowInteropHelper(w).Handle;
        SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x20 | 0x80000 | 0x80 | 0x08000000);
    }

    // Status baterai: (ada baterai?, sedang dicas / AC?, persen)
    public static (bool has, bool ac, int pct) Power()
    {
        if (!GetSystemPowerStatus(out var s)) return (false, false, 0);
        bool has = s.Flag != 128 && s.Pct != 255;
        return (has, s.AC == 1, s.Pct == 255 ? 0 : s.Pct);
    }

    // ---- CPU & RAM (halaman Sistem) ----
    [StructLayout(LayoutKind.Sequential)] struct FT { public uint Lo, Hi; }
    [StructLayout(LayoutKind.Sequential)]
    struct MEMSTAT
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FT idle, out FT kernel, out FT user);
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMSTAT m);

    static ulong prevIdle, prevKernel, prevUser;
    static bool cpuInit;
    static ulong Tk(FT f) => ((ulong)f.Hi << 32) | f.Lo;

    // Pemakaian CPU 0..1 sejak pemanggilan terakhir (pemanggilan pertama = 0)
    public static double CpuUsage()
    {
        if (!GetSystemTimes(out var i, out var k, out var u)) return 0;
        ulong ti = Tk(i), tk = Tk(k), tu = Tk(u);
        double r = 0;
        if (cpuInit)
        {
            double dTotal = (tk - prevKernel) + (tu - prevUser);      // kernel sudah termasuk idle
            double dIdle = ti - prevIdle;
            if (dTotal > 0) r = Math.Clamp(1 - dIdle / dTotal, 0, 1);
        }
        prevIdle = ti; prevKernel = tk; prevUser = tu; cpuInit = true;
        return r;
    }

    // (terpakai GB, total GB, persen 0..1)
    public static (double usedGb, double totalGb, double frac) Memory()
    {
        var m = new MEMSTAT { dwLength = (uint)Marshal.SizeOf<MEMSTAT>() };
        if (!GlobalMemoryStatusEx(ref m) || m.ullTotalPhys == 0) return (0, 0, 0);
        double total = m.ullTotalPhys / 1073741824.0, avail = m.ullAvailPhys / 1073741824.0;
        return (total - avail, total, Math.Clamp((total - avail) / total, 0, 1));
    }

    // Nama proses induk (untuk memastikan proses dijalankan dari terminal/shell)
    public static string? ParentName(int pid)
    {
        IntPtr h = OpenProcess(0x1000, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var i = new PBI();
            if (NtQueryInformationProcess(h, 0, ref i, Marshal.SizeOf<PBI>(), out _) != 0) return null;
            return Process.GetProcessById((int)i.Parent).ProcessName;
        }
        catch { return null; }
        finally { CloseHandle(h); }
    }
    // ---- Judul jendela gelap (untuk jendela Pengaturan) ----
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
    public static void DarkTitle(Window w)
    {
        try
        {
            var h = new WindowInteropHelper(w).Handle;
            int on = 1;
            DwmSetWindowAttribute(h, 20, ref on, 4);   // Windows 10 2004+ / 11
            DwmSetWindowAttribute(h, 19, ref on, 4);   // Windows 10 1809-1903
        }
        catch { }
    }

    // ---- Volume & mute sistem (Core Audio) ----
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCom { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        void RegisterControlChangeNotify(IntPtr p);
        void UnregisterControlChangeNotify(IntPtr p);
        void GetChannelCount(out uint n);
        void SetMasterVolumeLevel(float level, ref Guid ctx);
        void SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        void GetMasterVolumeLevel(out float level);
        void GetMasterVolumeLevelScalar(out float level);
        void SetChannelVolumeLevel(uint ch, float level, ref Guid ctx);
        void SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
        void GetChannelVolumeLevel(uint ch, out float level);
        void GetChannelVolumeLevelScalar(uint ch, out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    static IAudioEndpointVolume? endpoint;
    static long endpointTime, retryAt;

    // (berhasil?, volume 0..1, mute?) dari perangkat output default; endpoint diperbarui tiap 5 dtk
    public static (bool ok, float vol, bool mute) Volume()
    {
        long now = Environment.TickCount64;
        if (now < retryAt) return (false, 0, false);      // tidak ada perangkat audio: coba lagi nanti
        try
        {
            if (endpoint == null || now - endpointTime > 5000)
            {
                if (endpoint != null) { Marshal.ReleaseComObject(endpoint); endpoint = null; }
                var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                en.GetDefaultAudioEndpoint(0, 1, out var dev);          // eRender, eMultimedia
                var iid = typeof(IAudioEndpointVolume).GUID;
                dev.Activate(ref iid, 23, IntPtr.Zero, out var o);       // CLSCTX_ALL
                endpoint = (IAudioEndpointVolume)o;
                Marshal.ReleaseComObject(dev); Marshal.ReleaseComObject(en);
                endpointTime = now;
            }
            endpoint.GetMasterVolumeLevelScalar(out float v);
            endpoint.GetMute(out bool m);
            return (true, v, m);
        }
        catch { endpoint = null; retryAt = now + 3000; return (false, 0, false); }
    }
}
