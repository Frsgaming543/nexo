using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
namespace SnowIsland;

// Jendela terbuka (urut dari paling atas) -> dipakai untuk salju di tepi jendela
class WinInfo { public IntPtr H; public Rect R; public bool Taskbar, Zoomed; }

// Ikon desktop (posisi dalam piksel layar + nomor ikon di system image list)
class DeskIcon { public int Index, Image; public string Name = ""; public Rect R; }

static class Desk
{
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct PT { public int X, Y; }
    delegate bool EnumProc(IntPtr h, IntPtr l);

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool GetLayeredWindowAttributes(IntPtr h, out uint key, out byte alpha, out uint flags);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out PT p);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref PT p);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(int acc, bool inh, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr addr, int size, int type, int prot);
    [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr p, IntPtr addr, int size, int type);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p, IntPtr addr, byte[] buf, int size, out IntPtr n);
    [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(IntPtr p, IntPtr addr, byte[] buf, int size, out IntPtr n);

    public static bool Alive(IntPtr h) => IsWindow(h);

    // ---------- layar & kursor (piksel, ruang koordinat proses ini) ----------
    public static Rect Virtual()
    {
        var r = new Rect(GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79));
        if (r.Width < 100 || r.Height < 100) r = new Rect(0, 0, GetSystemMetrics(0), GetSystemMetrics(1));
        return r;
    }

    public static (double x, double y) CursorPx()
    {
        GetCursorPos(out var p);
        return (p.X, p.Y);
    }

    // ---------- daftar jendela ----------
    public static List<WinInfo> Windows()
    {
        var list = new List<WinInfo>();
        int me = Environment.ProcessId;
        var sb = new StringBuilder(64);
        EnumWindows((h, _) =>
        {
            try
            {
                if (!IsWindowVisible(h) || IsIconic(h)) return true;
                GetWindowThreadProcessId(h, out int pid);
                if (pid == me) return true;
                if (DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;   // DWMWA_CLOAKED
                sb.Clear(); GetClassName(h, sb, 64);
                string cls = sb.ToString();
                if (cls == "Progman" || cls == "WorkerW") return true;
                bool tray = cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd";
                if (!tray)
                {
                    int ex = GetWindowLong(h, -20);
                    if ((ex & 0x80) != 0 || (ex & 0x20) != 0 || (ex & 0x08000000) != 0) return true;   // toolwindow / click-through / noactivate
                    if (GetWindowTextLength(h) == 0) return true;
                    // jendela hantu / transparan (mis. pratinjau saat tab Chrome diseret): jangan diberi salju
                    if ((ex & 0x80000) != 0 && GetLayeredWindowAttributes(h, out uint ck, out byte al, out uint fl) && (fl & 2) != 0 && al < 120) return true;
                }
                RECT r;
                if (DwmGetWindowAttribute(h, 9, out r, 16) != 0)                                      // DWMWA_EXTENDED_FRAME_BOUNDS
                    if (!GetWindowRect(h, out r)) return true;
                double w = r.R - r.L, hh = r.B - r.T;
                if (w < 90 || hh < 28) return true;
                list.Add(new WinInfo { H = h, R = new Rect(r.L, r.T, w, hh), Taskbar = tray, Zoomed = IsZoomed(h) });
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return list;
    }

    // ---------- ikon desktop (baca SysListView32 milik explorer.exe) ----------
    static IntPtr lv;

    static IntPtr FindListView()
    {
        IntPtr def = FindWindowEx(FindWindow("Progman", null), IntPtr.Zero, "SHELLDLL_DefView", null);
        if (def == IntPtr.Zero)
        {
            IntPtr w = IntPtr.Zero;
            while ((w = FindWindowEx(IntPtr.Zero, w, "WorkerW", null)) != IntPtr.Zero)
            {
                def = FindWindowEx(w, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (def != IntPtr.Zero) break;
            }
        }
        return def == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(def, IntPtr.Zero, "SysListView32", null);
    }

    static long Send(IntPtr h, uint msg, int w, IntPtr l)
    {
        return SendMessageTimeout(h, msg, (IntPtr)w, l, 2, 250, out var r) == IntPtr.Zero ? 0 : (long)r;
    }

    // null = tidak bisa dibaca, list kosong = ikon desktop disembunyikan
    public static List<DeskIcon>? Icons()
    {
        try
        {
            if (IntPtr.Size != 8) return null;
            if (lv == IntPtr.Zero || !IsWindow(lv)) lv = FindListView();
            if (lv == IntPtr.Zero) return null;
            if (!IsWindowVisible(lv)) return new List<DeskIcon>();

            GetWindowThreadProcessId(lv, out int pid);
            IntPtr proc = OpenProcess(0x0008 | 0x0010 | 0x0020, false, pid);   // VM_OPERATION | VM_READ | VM_WRITE
            if (proc == IntPtr.Zero) return null;
            IntPtr mem = VirtualAllocEx(proc, IntPtr.Zero, 4096, 0x3000, 4);   // COMMIT | RESERVE, READWRITE
            if (mem == IntPtr.Zero) { CloseHandle(proc); return null; }
            try
            {
                var res = new List<DeskIcon>();
                int count = (int)Math.Min(Send(lv, 0x1004, 0, IntPtr.Zero), 120);   // LVM_GETITEMCOUNT
                var origin = new PT();
                ClientToScreen(lv, ref origin);
                var rb = new byte[16];
                var ib = new byte[88];
                var back = new byte[88];
                var tb = new byte[520];
                for (int i = 0; i < count; i++)
                {
                    // posisi ikon: LVM_GETITEMRECT, rc.left = LVIR_ICON (1)
                    Array.Clear(rb); BitConverter.GetBytes(1).CopyTo(rb, 0);
                    WriteProcessMemory(proc, mem, rb, 16, out _);
                    if (Send(lv, 0x100E, i, mem) == 0) continue;
                    ReadProcessMemory(proc, mem, rb, 16, out _);
                    int l = BitConverter.ToInt32(rb, 0), t = BitConverter.ToInt32(rb, 4), r = BitConverter.ToInt32(rb, 8), b = BitConverter.ToInt32(rb, 12);

                    // nama + nomor ikon: LVM_GETITEMW (LVITEMW 64-bit: iItem@4, pszText@24, cchTextMax@32, iImage@36)
                    Array.Clear(ib);
                    BitConverter.GetBytes(3).CopyTo(ib, 0);                 // LVIF_TEXT | LVIF_IMAGE
                    BitConverter.GetBytes(i).CopyTo(ib, 4);
                    BitConverter.GetBytes((long)(mem + 256)).CopyTo(ib, 24);
                    BitConverter.GetBytes(260).CopyTo(ib, 32);
                    WriteProcessMemory(proc, mem + 64, ib, 88, out _);
                    Send(lv, 0x104B, i, mem + 64);
                    ReadProcessMemory(proc, mem + 64, back, 88, out _);
                    ReadProcessMemory(proc, mem + 256, tb, 520, out _);
                    string name = Encoding.Unicode.GetString(tb);
                    int z = name.IndexOf('\0');
                    if (z >= 0) name = name.Substring(0, z);

                    res.Add(new DeskIcon
                    {
                        Index = i,
                        Name = name,
                        Image = BitConverter.ToInt32(back, 36),
                        R = new Rect(origin.X + l, origin.Y + t, Math.Max(0, r - l), Math.Max(0, b - t))
                    });
                }
                return res;
            }
            finally { VirtualFreeEx(proc, mem, 0, 0x8000); CloseHandle(proc); }
        }
        catch { return null; }
    }

    // ---------- click-through dinamis (mainan harus bisa diklik) ----------
    [DllImport("user32.dll", EntryPoint = "SetWindowLong")] static extern int SetWindowLongW(IntPtr h, int i, int v);

    public static void SetClickThrough(IntPtr h, bool on)
    {
        int ex = GetWindowLong(h, -20);
        ex = on ? (ex | 0x20) : (ex & ~0x20);
        SetWindowLongW(h, -20, ex);
    }
}
