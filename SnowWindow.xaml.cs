using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using WPath = System.Windows.Shapes.Path;

namespace NexoIsland;

// Satu jendela transparan yang menutupi SEMUA monitor:
//  - salju turun (didorong kursor, klik = ledakan)
//  - salju menumpuk di tepi atas jendela & taskbar; disapu kursor / jendela digoyang -> gumpalan salju jatuh
//  - bingkai Natal unik di setiap ikon desktop (ikon asli tetap terlihat)
//  - mainan: buldoser, kereta Santa, manusia salju susun-sendiri, pohon, kado (lihat SnowWindow.Toys.cs)
// Semua koordinat = piksel, relatif terhadap pojok kiri-atas layar virtual (ox, oy).
public partial class SnowWindow : Window
{
    class Flake { public Ellipse E = null!; public double X, Y, VX, VY, Fall, Phase, Freq, Flutter, D, Size; public bool Bokeh; public TranslateTransform T = new(); }
    class Spark { public Ellipse E = null!; public double X, Y, VX, VY, Life, Max; public TranslateTransform T = new(); public Cap? Ignore; }

    class Cap
    {
        public const double BW = 8;                      // lebar satu kolom salju (px)
        public IntPtr H; public WPath Shape = null!;
        public double Left, Top, Width;
        public double Vx, Vy, Shake; public bool HasPrev;   // deteksi jendela digoyang
        public double[] Lvl = Array.Empty<double>(), Tgt = Array.Empty<double>(), Ice = Array.Empty<double>();
        public List<(double a, double b)> Segs = new();  // bagian tepi atas yang tidak tertutup jendela lain
        public bool Dirty = true, Seen; public int Seed;
        public int Still;                                // berapa kali pembacaan berturut-turut jendela ini diam

        public void Reshape(double width)
        {
            Width = width;
            int n = (int)Math.Ceiling(width / BW) + 2;
            var rr = new Random(Seed);
            double s1 = rr.NextDouble() * 6.28, s2 = rr.NextDouble() * 6.28;
            var tgt = new double[n]; var ice = new double[n]; var lvl = new double[n];
            for (int i = 0; i < n; i++)
            {
                tgt[i] = 5 + 4.5 * (0.5 + 0.5 * Math.Sin(i * 0.17 + s1)) + 2.5 * (0.5 + 0.5 * Math.Sin(i * 0.43 + s2));
                ice[i] = rr.NextDouble() < 0.16 ? 4 + rr.NextDouble() * 6 : 0;
                if (i < Lvl.Length) lvl[i] = Lvl[i];
            }
            Tgt = tgt; Ice = ice; Lvl = lvl; Dirty = true;
        }

        public double HeightAt(double x)
        {
            double u = (x - Left) / BW; int i = (int)Math.Floor(u);
            if (i < 0 || i >= Lvl.Length - 1) return 0;
            double t = (1 - Math.Cos((u - i) * Math.PI)) * 0.5;      // interpolasi halus
            return Lvl[i] * (1 - t) + Lvl[i + 1] * t;
        }

        public void Add(double x, double amt)
        {
            int i = (int)Math.Round((x - Left) / BW);
            for (int k = -1; k <= 1; k++)
            {
                int j = i + k;
                if (j >= 0 && j < Lvl.Length) Lvl[j] = Math.Min(Lvl[j] + amt * (k == 0 ? 1 : 0.5), Tgt[j] + 14);
            }
            Dirty = true;
        }

        // pisau buldoser: kolom di depan pisau dibersihkan total; mengembalikan jumlah salju yang terdorong
        public double Plow(double x, double dir, double w)
        {
            double removed = 0, cx = x + dir * w * 0.3;
            int i0 = (int)Math.Floor((cx - w - Left) / BW), i1 = (int)Math.Ceiling((cx + w - Left) / BW);
            for (int i = Math.Max(0, i0); i <= Math.Min(Lvl.Length - 1, i1); i++)
            {
                if (Math.Abs(Left + i * BW - cx) > w) continue;
                removed += Lvl[i]; Lvl[i] = 0;
            }
            if (removed > 0) Dirty = true;
            return removed;
        }

        // mengembalikan jumlah salju yang terbuang (px tinggi, dijumlah semua kolom)
        public double Wipe(double x, double r, double amt)
        {
            double removed = 0;
            int i0 = (int)Math.Floor((x - r - Left) / BW), i1 = (int)Math.Ceiling((x + r - Left) / BW);
            for (int i = Math.Max(0, i0); i <= Math.Min(Lvl.Length - 1, i1); i++)
            {
                double f = 1 - Math.Abs(Left + i * BW - x) / r;
                if (f <= 0) continue;
                double nv = Math.Max(0, Lvl[i] - amt * f);
                removed += Lvl[i] - nv; Lvl[i] = nv;
            }
            Dirty = true;
            return removed;
        }
    }

    class Tile
    {
        public string Key = ""; public int Design;
        public Art.Costume? Cos; public MatrixTransform Mt = new();
        public bool Seen, WasInside;
        public double Hov, Ang, Vel, Op;
    }

    static Brush Solid(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    static readonly Brush Gold = Solid("#FFD866");
    static readonly Brush CapFill = MakeCapFill();
    static readonly Brush CapEdge = Solid("#F4F9FF");
    static readonly Brush SoftCore = MakeSoft(1.0, 0.85);     // serpihan salju: inti terang, tepi lembut
    static readonly Brush Bokeh = MakeSoft(0.30, 0.50);       // serpihan dekat yang buram (bokeh)
    static Brush MakeSoft(double centerA, double midA)
    {
        var g = new RadialGradientBrush();
        g.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * centerA), 255, 255, 255), 0));
        g.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * midA), 250, 252, 255), 0.55));
        g.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
        g.Freeze(); return g;
    }
    static Brush MakeCapFill()
    {
        var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        g.GradientStops.Add(new GradientStop(Colors.White, 0));
        g.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#CFE0F7"), 1));
        g.Freeze(); return g;
    }

    readonly List<Flake> flakes = new();
    readonly List<Spark> sparks = new();
    readonly List<Spark> chunks = new();               // gumpalan salju yang jatuh
    readonly Dictionary<IntPtr, Cap> caps = new();
    readonly Dictionary<string, Tile> tiles = new();
    readonly Dictionary<string, int> designOf = new();
    readonly Random rnd = new();
    readonly Canvas capLayer = new(), iconLayer = new(), toyLayer = new(), chunkLayer = new(), flakeLayer = new(), sparkLayer = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    volatile List<DeskIcon>? iconSnap;
    volatile List<WinInfo>? winSnap;
    volatile bool needWinsFlag;
    List<WinInfo> wins = new();
    IntPtr hwnd;
    double ox, oy, vw, vh, lastCx = double.NaN, lastCy, cursorVx, cursorVy;
    long lastWinMs = -1000, frame;
    int nextDesign;
    bool wasDown, winsFresh;

    public SnowWindow()
    {
        InitializeComponent();
        var v = Desk.Virtual();
        ox = v.X; oy = v.Y; vw = v.Width; vh = v.Height;

        // jendela menutupi seluruh layar virtual (semua monitor); isi digambar dalam piksel
        var d = VisualTreeHelper.GetDpi(this);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = ox / d.DpiScaleX; Top = oy / d.DpiScaleY;
        Width = vw / d.DpiScaleX; Height = vh / d.DpiScaleY;
        Sky.Width = vw; Sky.Height = vh;
        Sky.LayoutTransform = new ScaleTransform(1 / d.DpiScaleX, 1 / d.DpiScaleY);
        SourceInitialized += (_, _) => { Native.ClickThrough(this); hwnd = new WindowInteropHelper(this).Handle; };

        foreach (var l in new[] { capLayer, lightLayer, iconLayer, toyLayer, chunkLayer, flakeLayer, sparkLayer }) { l.IsHitTestVisible = false; Sky.Children.Add(l); }
        InitRoll();

        // salju berlapis kedalaman: jauh = kecil, lambat, redup; dekat = besar, cepat, terang; sebagian kecil bokeh buram
        int count = Math.Clamp((int)(vw * vh / 11000), 200, 520);
        for (int i = 0; i < count; i++)
        {
            double depth = Math.Pow(rnd.NextDouble(), 1.6);
            bool bokeh = rnd.NextDouble() < 0.04;
            double size = bokeh ? 9 + rnd.NextDouble() * 8 : 1.6 + depth * depth * 5.4;
            var f = new Flake
            {
                D = depth, Bokeh = bokeh, Size = size,
                E = new Ellipse { Width = size, Height = size, Fill = bokeh ? Bokeh : SoftCore, Opacity = bokeh ? 0.5 + rnd.NextDouble() * 0.4 : 0.4 + depth * 0.6 },
                X = rnd.NextDouble() * vw, Y = rnd.NextDouble() * vh,
                Fall = bokeh ? 2.4 + rnd.NextDouble() * 0.8 : 0.45 + depth * 1.9 + rnd.NextDouble() * 0.15,
                Phase = rnd.NextDouble() * 6.28, Freq = 0.015 + rnd.NextDouble() * 0.035,
                Flutter = 0.15 + (1 - depth) * 0.45 * rnd.NextDouble() + depth * 0.25
            };
            f.VY = f.Fall;
            f.E.RenderTransform = f.T;
            flakes.Add(f); flakeLayer.Children.Add(f.E);
        }
        for (int i = 0; i < 90; i++)
        {
            var s = new Spark { E = new Ellipse { Width = 3, Height = 3, Fill = Brushes.White, Visibility = Visibility.Collapsed } };
            s.E.RenderTransform = s.T;
            sparks.Add(s); sparkLayer.Children.Add(s.E);
        }
        for (int i = 0; i < 600; i++)
        {
            var s = new Spark { E = new Ellipse { Width = 5, Height = 5, Fill = SoftCore, Visibility = Visibility.Collapsed } };
            s.E.RenderTransform = s.T;
            chunks.Add(s); chunkLayer.Children.Add(s.E);
        }

        // ikon desktop dibaca di thread terpisah (lintas proses ke explorer.exe)
        var th = new Thread(() =>
        {
            while (true)
            {
                try { if (Prefs.Current.XmasIcons) iconSnap = Desk.Icons(); } catch { }
                Thread.Sleep(450);
            }
        }) { IsBackground = true };
        th.Start();

        // daftar jendela juga dibaca di thread terpisah supaya UI tidak tersendat saat jendela digeser
        var wth = new Thread(() =>
        {
            while (true)
            {
                try { if (needWinsFlag) winSnap = Desk.Windows(); } catch { }
                Thread.Sleep(12);
            }
        }) { IsBackground = true };
        wth.Start();

        ToyBox.Spawn += HandleSpawn;
        CompositionTarget.Rendering += (_, _) => Tick();
        Prefs.Changed += ApplyPrefs;
    }

    public void Start() => ApplyPrefs();

    // naikkan lagi ke paling atas (mis. setelah Mode Musik dibuka), tanpa merebut fokus
    public IntPtr Handle => hwnd;

    // Mode Musik terbuka: salju jatuh tetap tampil, tetapi salju di jendela, ikon Natal, lampu & mainan disembunyikan
    // (jendela di bawah Mode Musik tertutup layar gelap, jadi hiasannya tidak boleh muncul di depan)
    public bool MusicOpen { get; private set; }
    public void SetMusicOpen(bool on) { MusicOpen = on; SetSolid(false); ApplyPrefs(); }

    public void Raise() { if (!IsVisible) return; Topmost = false; Topmost = true; }

    // Salju / salju di jendela / ikon Natal bisa dinyalakan terpisah; jendela tetap tampil selama ada mainan
    void ApplyPrefs()
    {
        var p = Prefs.Current;
        bool any = p.Snow || p.SnowCaps || p.XmasIcons || toys.Count > 0 || HasLights;
        if (any) { if (!IsVisible) Show(); }
        else if (IsVisible) Hide();
        flakeLayer.Visibility = p.Snow ? Visibility.Visible : Visibility.Collapsed;
        bool mm = MusicOpen;
        capLayer.Visibility = p.SnowCaps && !mm ? Visibility.Visible : Visibility.Collapsed;
        iconLayer.Visibility = p.XmasIcons && !mm ? Visibility.Visible : Visibility.Collapsed;
        lightLayer.Visibility = toyLayer.Visibility = mm ? Visibility.Collapsed : Visibility.Visible;
    }

    void Tick()
    {
        if (!IsVisible) return;
        var p = Prefs.Current;
        frame++;
        var (px, py) = Desk.CursorPx();
        double cx = px - ox, cy = py - oy;
        bool down = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0;
        bool rdown = (Native.GetAsyncKeyState(0x02) & 0x8000) != 0;
        rollJustEnded = false;
        bool click = down && !wasDown; wasDown = down;
        double speed = double.IsNaN(lastCx) ? 0 : Math.Sqrt((cx - lastCx) * (cx - lastCx) + (cy - lastCy) * (cy - lastCy));
        cursorVx = double.IsNaN(lastCx) ? 0 : cx - lastCx;
        cursorVy = double.IsNaN(lastCx) ? 0 : cy - lastCy;
        lastCx = cx; lastCy = cy;
        if (MusicOpen)
        {
            // hanya salju jatuh (tanpa mendarat di jendela) + kilau
            SetSolid(false);
            if (p.Snow) UpdateFlakes(cx, cy, false);
            UpdateChunks(false);
            UpdateSparks();
            return;
        }
        UpdateRollTrigger(cx, cy, p);

        long now = clock.ElapsedMilliseconds;
        bool needWins = p.SnowCaps || p.XmasIcons || toys.Count > 0 || HasLights;
        needWinsFlag = needWins;
        winsFresh = false;
        var snapW = winSnap;
        if (needWins && snapW != null && !ReferenceEquals(snapW, wins)) { wins = snapW; winsFresh = true; }

        if (p.SnowCaps || toys.Count > 0 || HasLights) UpdateCaps(cx, cy, click && !ToyBusy(), speed);
        else if (caps.Count > 0) ClearCaps();
        if (p.XmasIcons) UpdateIcons(cx, cy, click && !ToyBusy(), now);
        UpdateStrands(cx, cy, rdown);
        UpdateLinks(cx, cy, click, rdown);
        UpdateToys(cx, cy, down, click, rdown);
        UpdateRoll(cx, cy, click);
        if (p.Snow) UpdateFlakes(cx, cy, click && !ToyBusy());
        UpdateChunks(p.SnowCaps || toys.Count > 0);
        UpdateSparks();
    }

    // ===================== Salju turun =====================
    void RespawnFlake(Flake f)
    {
        f.Y = -f.Size - rnd.NextDouble() * 30; f.X = rnd.NextDouble() * vw; f.VX = 0; f.VY = f.Fall;
    }

    void UpdateFlakes(double cx, double cy, bool click)
    {
        bool land = Prefs.Current.SnowCaps && !MusicOpen;
        double t = clock.Elapsed.TotalSeconds;
        // angin: hembusan lambat + gust yang datang dan pergi
        double gust = Math.Sin(t * 0.21) * 0.45 + Math.Sin(t * 0.53 + 1.7) * 0.3 + Math.Max(0, Math.Sin(t * 0.09 + 0.6)) * 1.2;
        foreach (var f in flakes)
        {
            double dx = f.X - cx, dy = f.Y - cy, d = Math.Sqrt(dx * dx + dy * dy) + 0.001;
            if (rollOn)
            {
                // mode gulung: salju tertarik ke kursor dan terkumpul jadi bola
                if (!f.Bokeh && d < 160) { double k = (160 - d) / 160; f.VX -= dx / d * k * 0.7; f.VY -= dy / d * k * 0.7; }
                if (!f.Bokeh && d < 55) { rollAmt += 0.25 + f.D * 0.3; RespawnFlake(f); continue; }
            }
            else
            {
                if (d < 110) { double k = (110 - d) / 110; f.VX += dx / d * k * 1.3; f.VY += dy / d * k * 1.3; }          // kursor mendorong salju
                if (click && d < 260) { double k = (260 - d) / 260; f.VX += dx / d * k * 14; f.VY += dy / d * k * 14; } // klik = ledakan salju
            }

            f.Phase += f.Freq;
            double local = Math.Sin(f.X * 0.0045 + t * 0.6 + f.Y * 0.0021) * 0.35;
            double tvx = (gust + local) * (0.3 + f.D * 0.9) + Math.Sin(f.Phase) * f.Flutter;     // terbawa angin + melayang kiri-kanan
            double tvy = f.Fall * (1 + 0.2 * Math.Sin(f.Phase * 0.7 + 1.3));
            f.VX += (tvx - f.VX) * 0.04;
            f.VY += (tvy - f.VY) * 0.04;
            double prevY = f.Y;
            f.X += f.VX; f.Y += f.VY;

            if (land && !f.Bokeh && f.VY > 0 && TryLand(f, prevY)) continue;    // mendarat di tepi jendela

            if (f.Y > vh + 10) RespawnFlake(f);
            if (f.Y < -300) f.Y = -f.Size;
            if (f.X > vw + 10) f.X = -10; else if (f.X < -10) f.X = vw + 10;
            f.T.X = f.X - f.Size / 2; f.T.Y = f.Y - f.Size / 2;
        }
    }

    Cap? HitCap(double x, double prevY, double y, Cap? ignore = null)
    {
        foreach (var c in caps.Values)
        {
            if (c == ignore || c.Still < 12) continue;      // jendela yang sedang bergerak tidak menampung salju
            foreach (var (a, b) in c.Segs)
            {
                if (x < a + 3 || x > b - 3) continue;
                double surface = c.Top - c.HeightAt(x);
                if (prevY <= surface && y >= surface && y < c.Top + 16) return c;
            }
        }
        return null;
    }

    bool TryLand(Flake f, double prevY)
    {
        var c = HitCap(f.X, prevY, f.Y);
        if (c == null) return false;
        c.Add(f.X, 0.8 + f.D * 2.0);
        RespawnFlake(f);
        return true;
    }

    // ===================== Salju di tepi jendela =====================
    void UpdateCaps(double cx, double cy, bool click, double speed)
    {
        foreach (var c in caps.Values) c.Seen = false;

        for (int i = 0; i < wins.Count; i++)
        {
            var w = wins[i];
            if (w.Zoomed || w.R.Top < oy + 2) continue;      // jendela maximize / tepi atas di luar layar: tanpa salju

            if (!caps.TryGetValue(w.H, out var cap))
            {
                cap = new Cap { H = w.H, Seed = rnd.Next() };
                cap.Shape = new WPath { Fill = CapFill, Stroke = CapEdge, StrokeThickness = 1, IsHitTestVisible = false };
                capLayer.Children.Add(cap.Shape);
                caps[w.H] = cap;
            }
            cap.Seen = true;

            double left = w.R.Left - ox, top = w.R.Top - oy, width = w.R.Width;
            if (cap.Lvl.Length == 0 || Math.Abs(width - cap.Width) > 0.5) cap.Reshape(width);

            // deteksi goyangan: bolak-balik arah gerak jendela
            if (winsFresh)
            {
                if (cap.HasPrev)
                {
                    double vx = left - cap.Left, vy = top - cap.Top;
                    if (vx * cap.Vx + vy * cap.Vy < 0) cap.Shake += Math.Sqrt(vx * vx + vy * vy) + Math.Sqrt(cap.Vx * cap.Vx + cap.Vy * cap.Vy);
                    cap.Vx = vx; cap.Vy = vy;
                    double sp = Math.Sqrt(vx * vx + vy * vy);
                    if (sp > 0.6)
                    {
                        cap.Still = 0;
                        SpillMoving(cap, cap.Left, cap.Top, vx, vy, sp);         // jendela digeser: salju langsung tumpah & jatuh (tidak melayang)
                        CarryToys(cap, vx, vy);                                  // mainan di atasnya ikut terbawa
                    }
                    else cap.Still++;
                }
                cap.Shake *= 0.85;
            }
            cap.HasPrev = true;
            if (left != cap.Left || top != cap.Top) cap.Dirty = true;
            cap.Left = left; cap.Top = top;

            // bagian tepi atas yang tidak tertutup jendela lain di atasnya
            var segs = new List<(double a, double b)> { (left, left + width) };
            for (int j = 0; j < i && segs.Count > 0; j++)
            {
                var o = wins[j];
                double ot = o.R.Top - oy, ob = o.R.Bottom - oy;
                if (ob < top - 18 || ot > top + 2) continue;
                segs = Subtract(segs, o.R.Left - ox, o.R.Right - ox);
            }
            segs.RemoveAll(s => s.b - s.a < 24);
            if (!SameSegs(segs, cap.Segs)) { cap.Segs = segs; cap.Dirty = true; }

            if (cap.Shake > 28) { ShakeOff(cap); ShakeToys(cap); cap.Shake = 0; }

            // interaksi: sapu kursor & klik menyekop salju -> salju jatuh sebagai gumpalan
            foreach (var (a, b) in cap.Segs)
            {
                if (cx < a - 40 || cx > b + 40) continue;
                double dy = top - cy;
                if (click && !rollOn && dy > -14 && dy < 70)
                {
                    double rem = cap.Wipe(cx, 70, 9);
                    SpawnChunks(cx, top - 6, Math.Min(36, (int)(rem / 1.2) + 4), cursorVx * 0.3, 4);
                    Spray(cx, top - 6, 10, 5, true);
                }
                else if (speed > 4 && (rollOn || !Prefs.Current.AutoRoll) && dy > (rollOn ? -30 : -6) && dy < (rollOn ? 60 : 38) && cx >= a && cx <= b)
                {
                    double rem = cap.Wipe(cx, rollOn ? 34 : 26, Math.Min(2.5, speed * 0.12));
                    if (rollOn) rollAmt += rem * 0.9;                       // dikumpulkan jadi bola salju di kursor
                    else if (rem > 0.4) SpawnChunks(cx, top - cap.HeightAt(cx), Math.Min(6, (int)(rem / 1.0) + 1), cursorVx * 0.35, 2);
                }
            }

            // salju tumbuh kembali ke bentuk dasar; tumpukan ekstra mencair pelan
            bool changed = false, still = cap.Still > 30;
            for (int k = 0; k < cap.Lvl.Length; k++)
            {
                double t = cap.Tgt[k], v = cap.Lvl[k];
                if (v < t) { if (still) { v = Math.Min(t, v + 0.015); changed = true; } }
                else if (v > t) { v = Math.Max(t, v - 0.0015); changed = true; }
                cap.Lvl[k] = v;
            }
            if (changed) cap.Dirty = true;

            bool showCap = cap.Segs.Count > 0 && cap.Still >= 6;      // selama jendela bergerak, lapisan salju disembunyikan
            cap.Shape.Visibility = showCap ? Visibility.Visible : Visibility.Collapsed;
            if (cap.Dirty && showCap)
            {
                cap.Dirty = false;
                cap.Shape.Data = BuildCap(cap);
            }
        }

        // jendela ditutup / diminimize / di-maximize -> seluruh salju di atasnya jatuh ke bawah
        foreach (var key in caps.Where(kv => !kv.Value.Seen).Select(kv => kv.Key).ToList())
        {
            ReleaseCap(caps[key]);
            capLayer.Children.Remove(caps[key].Shape);
            caps.Remove(key);
        }
    }

    void ClearCaps()
    {
        foreach (var c in caps.Values) capLayer.Children.Remove(c.Shape);
        caps.Clear();
    }

    void ReleaseCap(Cap c)
    {
        if (c.Segs.Count == 0) return;
        for (int i = 0; i < c.Lvl.Length; i++)
        {
            double x = c.Left + i * Cap.BW;
            if (c.Lvl[i] < 0.6 || !InSegs(c, x)) continue;
            int n = Math.Max(1, (int)(c.Lvl[i] / 3));
            for (int k = 0; k < n; k++)
                SpawnChunks(x + rnd.NextDouble() * Cap.BW, c.Top - rnd.NextDouble() * c.Lvl[i], 1, (rnd.NextDouble() - 0.5) * 1.2, 0.8);
        }
        for (int k = 0; k < 14; k++)   // debu salju halus
        {
            double x = c.Left + rnd.NextDouble() * c.Width;
            if (InSegs(c, x)) Spray(x, c.Top - 4, 1, 1.2, false);
        }
    }

    static bool InSegs(Cap c, double x)
    {
        foreach (var (a, b) in c.Segs) if (x >= a && x <= b) return true;
        return false;
    }

    // jendela sedang digeser: salju di tepinya langsung lepas dan jatuh (membawa sebagian laju jendela), tidak ada yang tertinggal melayang
    void SpillMoving(Cap c, double left, double top, double vx, double vy, double sp)
    {
        double frac = Math.Min(1, 0.45 + sp * 0.05);
        for (int i = 0; i < c.Lvl.Length; i++)
        {
            if (c.Lvl[i] < 0.15) { c.Lvl[i] = 0; continue; }
            double x = left + i * Cap.BW;
            if (!InSegs(c, x)) continue;
            double removed = c.Lvl[i] * frac;
            c.Lvl[i] -= removed;
            if (c.Lvl[i] < 0.3) c.Lvl[i] = 0;
            double e = removed / 1.8; int n = (int)e;
            if (rnd.NextDouble() < e - n) n++;
            if (n > 0) SpawnChunks(x + rnd.NextDouble() * Cap.BW, top - c.Lvl[i] - 1, n, vx * 0.85, 0.9, c, vy * 0.5);
        }
        c.Dirty = true;
    }

    // jendela digoyang -> gumpalan salju rontok
    void ShakeOff(Cap c)
    {
        int spawned = 0;
        for (int i = 0; i < c.Lvl.Length && spawned < 70; i++)
        {
            if (c.Lvl[i] < 1.5 || rnd.NextDouble() > 0.6) continue;
            double x = c.Left + i * Cap.BW;
            if (!InSegs(c, x)) continue;
            double removed = c.Lvl[i] * (0.6 + rnd.NextDouble() * 0.4);
            c.Lvl[i] -= removed;
            int n = Math.Max(1, (int)(removed / 2.2));
            SpawnChunks(x, c.Top - c.Lvl[i], n, c.Vx * 0.25, 1.8, c);
            spawned += n;
        }
        c.Dirty = true;
    }

    static List<(double a, double b)> Subtract(List<(double a, double b)> segs, double l, double r)
    {
        var res = new List<(double a, double b)>();
        foreach (var (a, b) in segs)
        {
            if (r <= a || l >= b) { res.Add((a, b)); continue; }
            if (l > a) res.Add((a, l));
            if (r < b) res.Add((r, b));
        }
        return res;
    }

    static bool SameSegs(List<(double a, double b)> x, List<(double a, double b)> y)
    {
        if (x.Count != y.Count) return false;
        for (int i = 0; i < x.Count; i++)
            if (Math.Abs(x[i].a - y[i].a) > 0.5 || Math.Abs(x[i].b - y[i].b) > 0.5) return false;
        return true;
    }

    static Geometry BuildCap(Cap c)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            foreach (var (a, b) in c.Segs)
            {
                var pts = new List<Point>();
                ctx.BeginFigure(new Point(a, c.Top + 2), true, true);
                for (double x = a; x < b + 4; x += 4)
                {
                    double xx = Math.Min(x, b);
                    double e = Math.Min(1, Math.Min((xx - a) / 14, (b - xx) / 14));
                    e = e * e * (3 - 2 * e);
                    pts.Add(new Point(xx, c.Top - c.HeightAt(xx) * e));
                }
                pts.Add(new Point(b, c.Top + 2));
                ctx.PolyLineTo(pts, true, true);

                for (int i = 0; i < c.Ice.Length; i++)
                {
                    if (c.Ice[i] <= 0) continue;
                    double x = c.Left + i * Cap.BW;
                    if (x < a + 6 || x > b - 6) continue;
                    double ratio = c.Tgt[i] > 0 ? Math.Min(1, c.Lvl[i] / c.Tgt[i]) : 0;
                    double len = c.Ice[i] * ratio;
                    if (len < 1.5) continue;
                    ctx.BeginFigure(new Point(x - 2.5, c.Top + 1), true, true);
                    ctx.LineTo(new Point(x + 2.5, c.Top + 1), true, true);
                    ctx.LineTo(new Point(x + 0.3, c.Top + 1 + len), true, true);
                }
            }
        }
        g.Freeze();
        return g;
    }

    // ===================== Bingkai Natal di ikon desktop =====================
    static string KeyOf(DeskIcon i) => i.Name.Length > 0 ? i.Name : "#" + i.Index;

    void UpdateIcons(double cx, double cy, bool click, long now)
    {
        foreach (var t in tiles.Values) t.Seen = false;
        var snap = iconSnap;
        if (snap != null)
        {
            // kostum dibagi berurutan (nama A-Z) supaya setiap ikon berbeda sampai kostum habis
            var fresh = snap.Select(KeyOf).Where(n => !designOf.ContainsKey(n)).Distinct()
                            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
            foreach (var n in fresh) designOf[n] = nextDesign++ % Art.Count;

            double sx = cx + ox, sy = cy + oy;       // kursor dalam piksel layar
            foreach (var ic in snap)
            {
                double s = Math.Min(ic.R.Width, ic.R.Height);
                if (s < 16 || s > 200) continue;
                string key = KeyOf(ic);
                if (!tiles.TryGetValue(key, out var t))
                {
                    t = new Tile { Key = key, Design = designOf[key] };
                    t.Cos = Art.Build(t.Design);
                    t.Cos.Root.RenderTransform = t.Mt;
                    t.Cos.Root.Opacity = 0;
                    iconLayer.Children.Add(t.Cos.Root);
                    tiles[key] = t;
                }
                t.Seen = true;

                // tertutup jendela? (ikon desktop ada di bawah semua jendela)
                var hit = new Rect(ic.R.X - 14, ic.R.Y - s * 0.7, ic.R.Width + 28, ic.R.Height + s * 0.7 + 14);
                bool covered = false;
                foreach (var w in wins) if (w.R.IntersectsWith(hit)) { covered = true; break; }

                var hov = ic.R; hov.Inflate(4, 4);
                bool inside = !covered && hov.Contains(sx, sy);
                t.Hov += ((inside ? 1 : 0) - t.Hov) * 0.2;
                if (inside && !t.WasInside) t.Vel += 2.2;
                t.WasInside = inside;

                double ccx = ic.R.X + ic.R.Width / 2 - ox, ccy = ic.R.Y + ic.R.Height / 2 - oy;
                if (click && inside) { t.Vel += 5; Spray(ccx, ccy - s * 0.5, 12, 4, true, Gold); }

                t.Ang += t.Vel; t.Vel += -t.Ang * 0.12; t.Vel *= 0.9;      // aksesori bergoyang seperti pegas

                // bingkai: statis tepat di sekeliling ikon asli (kotak 0..100 -> s x s)
                double k = s / 100;
                var m = Matrix.Identity;
                m.Scale(k, k);
                m.Translate(ccx - s / 2, ccy - s / 2);
                t.Mt.Matrix = m;

                // aksesori (topi, tanduk, ...): goyang + melompat saat hover
                var tm = Matrix.Identity;
                tm.RotateAt(t.Ang, 50, -10);
                tm.Translate(0, -6 * t.Hov);
                t.Cos!.TopMt.Matrix = tm;

                t.Op += ((covered ? 0 : 1) - t.Op) * 0.2;
                var root = t.Cos.Root;
                root.Opacity = t.Op;
                root.Visibility = t.Op < 0.03 ? Visibility.Collapsed : Visibility.Visible;
                for (int j = 0; j < t.Cos.Blink.Count; j++)
                    t.Cos.Blink[j].Opacity = ((frame / 25 + j) & 1) == 0 ? 1 : 0.3;     // lampu berkedip
            }
        }

        foreach (var key in tiles.Where(kv => !kv.Value.Seen).Select(kv => kv.Key).ToList())
        {
            if (tiles[key].Cos != null) iconLayer.Children.Remove(tiles[key].Cos!.Root);
            tiles.Remove(key);
        }
    }

    // ===================== Percikan, kilau & gumpalan salju =====================
    void Spray(double x, double y, int n, double power, bool big, Brush? fill = null)
    {
        foreach (var s in sparks)
        {
            if (n <= 0) break;
            if (s.Life > 0) continue;
            s.X = x + (rnd.NextDouble() - 0.5) * 10; s.Y = y;
            s.VX = (rnd.NextDouble() - 0.5) * power * 2; s.VY = -(1 + rnd.NextDouble() * power);
            s.Max = s.Life = 30 + rnd.NextDouble() * 40;
            s.E.Width = s.E.Height = 2 + rnd.NextDouble() * (big ? 3 : 1.5);
            s.E.Fill = fill ?? Brushes.White;
            s.E.Visibility = Visibility.Visible;
            n--;
        }
    }

    void UpdateSparks()
    {
        foreach (var s in sparks)
        {
            if (s.Life <= 0) continue;
            s.Life--; s.VY += 0.18; s.VX *= 0.98; s.X += s.VX; s.Y += s.VY;
            if (s.Life <= 0) { s.E.Visibility = Visibility.Collapsed; continue; }
            s.E.Opacity = Math.Min(1, s.Life / s.Max * 1.5);
            s.T.X = s.X; s.T.Y = s.Y;
        }
    }

    // gumpalan salju: jatuh karena gravitasi; kalau mendarat di jendela di bawahnya, menambah tumpukan di sana
    void SpawnChunks(double x, double y, int n, double vx, double spread, Cap? from = null, double vy = 0)
    {
        foreach (var s in chunks)
        {
            if (n <= 0) break;
            if (s.Life > 0) continue;
            s.X = x + (rnd.NextDouble() - 0.5) * 12; s.Y = y;
            s.VX = vx + (rnd.NextDouble() - 0.5) * spread * 2; s.VY = vy - rnd.NextDouble() * spread;
            s.Life = s.Max = 500; s.Ignore = from;
            s.E.Width = s.E.Height = 5 + rnd.NextDouble() * 6;
            s.E.Visibility = Visibility.Visible;
            n--;
        }
    }

    void UpdateChunks(bool land)
    {
        foreach (var s in chunks)
        {
            if (s.Life <= 0) continue;
            s.Life--;
            double prevY = s.Y;
            s.VY += 0.35; s.VX *= 0.995; s.X += s.VX; s.Y += s.VY;
            bool dead = s.Life <= 0 || s.Y > vh + 20 || s.X < -50 || s.X > vw + 50;
            if (!dead && rollOn && Math.Abs(s.X - lastCx) < 60 && Math.Abs(s.Y - lastCy) < 60) { rollAmt += 0.7; dead = true; }
            if (!dead && land && s.VY > 0)
            {
                var c = HitCap(s.X, prevY, s.Y, s.Max - s.Life < 70 ? s.Ignore : null);
                if (c != null)
                {
                    c.Add(s.X, 1.6); dead = true;
                    if (rnd.NextDouble() < 0.35) Spray(s.X, s.Y - 2, 2, 1.1, false);
                }
            }
            if (dead) { s.Life = 0; s.E.Visibility = Visibility.Collapsed; continue; }
            s.T.X = s.X; s.T.Y = s.Y;
        }
    }
}
