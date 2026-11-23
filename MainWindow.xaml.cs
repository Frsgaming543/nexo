using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Media.Control;
using Ellipse = System.Windows.Shapes.Ellipse;
using Line = System.Windows.Shapes.Line;
using WPath = System.Windows.Shapes.Path;

namespace NexoIsland;

public partial class MainWindow : Window
{
    // ===== Pengaturan =====
    const double CW = 190, CH = 48, EW = 640, LW = 790, LH = 236;
    // tinggi island tiap halaman: musik, shalat, fokus, to-do, kalender, cuaca, sistem
    static readonly double[] PageH = { 236, 282, 268, 268, 268, 268, 268 };

    static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(8) };
    static readonly Brush Cyan = new SolidColorBrush(Color.FromRgb(0x7F, 0xDB, 0xFF));
    static readonly Brush Pink = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0xE3));
    static readonly Brush Warm = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x9A));
    static readonly Brush Drowsy = new SolidColorBrush(Color.FromRgb(0x6E, 0x93, 0xC9));
    static readonly Brush Dim = new SolidColorBrush(Color.FromRgb(0x9C, 0xC9, 0xF0));
    static readonly Brush CardBg = new SolidColorBrush(Color.FromArgb(0x1A, 255, 255, 255));
    static readonly Brush CardEdge = new SolidColorBrush(Color.FromArgb(0x16, 255, 255, 255));
    static readonly Brush CardHi = new LinearGradientBrush(Color.FromRgb(0x3A, 0x86, 0xFF), Color.FromRgb(0x16, 0x3E, 0xB0), 90);
    static readonly Brush CardHiEdge = new SolidColorBrush(Color.FromArgb(0x88, 0xBF, 0xE9, 0xFF));
    static readonly Color BlueGlow = Color.FromRgb(0x23, 0x78, 0xFF);   // biru menyala (default semua island)

    readonly Random rnd = new();
    readonly Border[] bars = new Border[5];
    readonly Stopwatch sw = Stopwatch.StartNew();

    // ===== Tema warna: atas hitam, bawah gradient menyala (biru / warna cover lagu) =====
    readonly LinearGradientBrush bgBrush = new() { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
    readonly LinearGradientBrush edgeBrush = new() { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
    readonly RadialGradientBrush haloBrush = new() { Center = new Point(0.5, 0.15), GradientOrigin = new Point(0.5, 0.15), RadiusX = 0.55, RadiusY = 0.9 };
    readonly SolidColorBrush accentBr = new(Color.FromRgb(0x8F, 0xC4, 0xFF));
    GradientStop bg1 = null!, bg2 = null!, bg3m = null!, bg3 = null!, ed1 = null!, ed2 = null!, h0 = null!, h1 = null!;
    Color curBot = BlueGlow, curBot2 = BlueGlow, appliedBot = Colors.Transparent, appliedBot2 = Colors.Transparent, songBot = BlueGlow;
    Color userC1 = BlueGlow, userC2 = BlueGlow;      // warna pilihan pengguna (dari Pengaturan)
    bool appliedGrad = true, appliedTwo, lastSnow = true;
    string loadedCity = "", loadedCountry = "";
    SettingsWindow? settingsWin;
    bool hasArt;

    // ukuran (pegas) & lapisan
    double w = CW, h = CH, vw, vh, tw = CW, th = CH, ex, last;
    double opEyes = 1, opLyric, opToast, opTerm, opFocus, opHud, toastUntil, hudUntil, hudShown, hudTarget;
    bool expanded, lyricsMode, lyricPinned, happy, blink, playing;
    int page;

    // bot
    Point lastCur; double lastMove, lookPrevX, wakeUntil;
    double hatAng, hatVel, pomAng, pomVel, hatLift, hatLiftV, cupOp, cupAng, zOp, nOp;
    Brush? lastCol;

    // musik
    GlobalSystemMediaTransportControlsSessionManager? mgr;
    string lastKey = "", lyricState = "";
    List<(double t, string l)>? lyrics;

    // jadwal shalat
    readonly string[] pKeys = { "Fajr", "Dhuhr", "Asr", "Maghrib", "Isha" };
    readonly string[] pNames = { "Subuh", "Dzuhur", "Ashar", "Maghrib", "Isya" };
    readonly string[] pIcons = { "🌅", "☀", "🌤", "🌇", "🌙" };
    readonly Border[] pCards = new Border[5];
    readonly TextBlock[] pTimes = new TextBlock[5], pLabels = new TextBlock[5];
    readonly Ellipse[] pDots = new Ellipse[5];
    const double Cx = 110, Cy = 92, R = 84;      // sun arc
    WPath arcDone = null!;
    Ellipse sun = null!, sunGlow = null!;
    TextBlock moon = null!;
    Dictionary<string, TimeSpan>? prayers;
    DateTime prayerDate;
    string notified = "";

    // fokus (pomodoro)
    readonly int[][] presets = { new[] { 25, 5 }, new[] { 50, 10 }, new[] { 15, 3 } };
    readonly Ellipse[] fDots = new Ellipse[4];
    int presetIdx, focusLeft = 25 * 60, focusTotal = 25 * 60, focusDone;
    bool focusRunning, focusBreak;

    // to-do
    class Todo { public string Text { get; set; } = ""; public bool Done { get; set; } public int Pri { get; set; } }
    List<Todo> todos = new();
    static readonly string TodoPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NexoIsland", "todos.json");

    // jaringan, baterai & terminal
    string? lastSsid; int netTick;
    bool? lastAc, lastCaps, lastNum, lastMute; bool lowWarned;
    float? lastVol;
    int timePhase = -1;
    const int PageCount = 7;
    static readonly HashSet<string> Dev = new(StringComparer.OrdinalIgnoreCase) { "dotnet", "node", "python", "py", "java", "cargo", "go", "ruby", "php", "deno", "bun" };
    static readonly HashSet<string> Shells = new(StringComparer.OrdinalIgnoreCase) { "cmd", "powershell", "pwsh", "bash", "wsl", "nu", "sh", "zsh", "fish", "WindowsTerminal", "OpenConsole", "conhost" };
    readonly HashSet<int> baseline = new();
    int termPid = -1; string? termName; DateTime termStart;

    public MainWindow()
    {
        InitializeComponent();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NexoIsland/1.0");
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;

        InitTheme();
        ApplyPrefs(); Prefs.Changed += ApplyPrefs;
        for (int i = 0; i < 5; i++)
        {
            bars[i] = new Border { Width = 3, Height = 4, CornerRadius = new CornerRadius(1.5), Background = accentBr,
                                   Margin = new Thickness(i == 0 ? 0 : 3, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Eq.Children.Add(bars[i]);
        }
        BuildArc(); BuildPrayerCards(); BuildDock();
        BuildFocusUi(); BuildTodoUi(); BuildCalendarUi(); BuildSystemUi();
        UpdateFocusUI(); LoadTodos(); LoadEvents(); SetPage(0);

        CompositionTarget.Rendering += (_, _) => Frame();

        var blinkT = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        blinkT.Tick += async (_, _) =>
        {
            blink = true; await Task.Delay(130); blink = false;
            blinkT.Interval = TimeSpan.FromSeconds(2 + rnd.NextDouble() * 3);
        };
        blinkT.Start();

        var clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        clock.Tick += (_, _) => SecondTick();
        clock.Start(); SecondTick();

        var fast = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        fast.Tick += (_, _) => { TickProgress(); KeysAndVolumeCheck(); };
        fast.Start();

        var media = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        media.Tick += async (_, _) => await Refresh();
        media.Start();

        foreach (var p in Process.GetProcesses()) baseline.Add(p.Id);
        var term = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        term.Tick += (_, _) => ScanTerminal();
        term.Start();

        Loaded += async (_, _) =>
        {
            try { mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); } catch { }
            StartWifi(); StartBluetooth();
            _ = LoadPrayers();
            _ = LoadWeather();
            _ = Task.Run(async () => { await Task.Delay(2500); XmasGreeting(); });
            _ = Task.Run(async () => { await Task.Delay(8500); Dispatcher.Invoke(() => CalendarGreeting()); });
            await Refresh();
        };
    }

    // ===================== Tema warna =====================
    static Color Mix(Color a, Color b, double t) => Theme.Mix(a, b, t);

    // Transisi warna halus; selalu bergerak minimal 1 level per frame supaya tepat sampai ke warna target
    static Color Ease(Color a, Color b, double t)
    {
        static byte C(byte x, byte y, double k)
        {
            double d = y - x;
            if (d == 0) return y;
            double st = d * k;
            if (Math.Abs(st) < 1) st = Math.Sign(d);
            return (byte)Math.Clamp(Math.Round(x + st), 0, 255);
        }
        return Color.FromRgb(C(a.R, b.R, t), C(a.G, b.G, t), C(a.B, b.B, t));
    }
    static int ColorDiff(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

    void InitTheme()
    {
        var g0 = new GradientStop(Colors.Black, 0);
        bg1 = new GradientStop(Colors.Black, 0.28);
        bg2 = new GradientStop(Colors.Black, 0.68);
        bg3m = new GradientStop(Colors.Black, 0.84);
        bg3 = new GradientStop(BlueGlow, 1);
        foreach (var s in new[] { g0, bg1, bg2, bg3m, bg3 }) bgBrush.GradientStops.Add(s);

        var e0 = new GradientStop(Color.FromArgb(0x00, 255, 255, 255), 0);
        ed1 = new GradientStop(Color.FromArgb(0x1C, 255, 255, 255), 0.4);
        ed2 = new GradientStop(Colors.White, 1);
        foreach (var s in new[] { e0, ed1, ed2 }) edgeBrush.GradientStops.Add(s);

        h0 = new GradientStop(BlueGlow, 0); h1 = new GradientStop(BlueGlow, 1);
        haloBrush.GradientStops.Add(h0); haloBrush.GradientStops.Add(h1);

        Bg.Fill = bgBrush; Bg.Stroke = edgeBrush; Halo.Background = haloBrush;
        Fill.Background = accentBr; PrayerBar.Background = accentBr; FocusArc.Stroke = accentBr;
        var ps = Prefs.Current;
        curBot = ps.C1; curBot2 = ps.TwoColors ? ps.C2 : ps.C1;
        ApplyTheme(curBot, curBot2, ps.TwoColors);
    }

    // Warna island: atas selalu hitam, makin ke bawah menyala dengan warna 1 -> warna 2
    // (gradient dimatikan = hitam polos; warna aksen tetap mengikuti warna pilihan)
    void ApplyTheme(Color c1, Color c2, bool two)
    {
        bool grad = Prefs.Current.GradientEnabled;
        if (grad)
        {
            bg2.Color = Theme.Stop68(c1);
            bg3m.Color = Theme.Stop84(c1, c2, two);
            bg3.Color = c2;
        }
        else bg2.Color = bg3m.Color = bg3.Color = Colors.Black;

        var bright = Mix(c2, Colors.White, 0.35);
        ed2.Color = grad ? Color.FromArgb(0xE6, bright.R, bright.G, bright.B) : Color.FromArgb(0x55, 255, 255, 255);
        h0.Color = Color.FromArgb(0x96, c2.R, c2.G, c2.B);
        h1.Color = Color.FromArgb(0x00, c2.R, c2.G, c2.B);
        accentBr.Color = Mix(grad ? c2 : c1, Colors.White, 0.4);
        appliedBot = c1; appliedBot2 = c2; appliedGrad = grad; appliedTwo = two;
    }

    static void RgbToHsv(double r, double g, double b, out double h, out double s, out double v)
    {
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        v = max; s = max <= 0 ? 0 : d / max;
        if (d <= 0) h = 0;
        else if (max == r) h = 60 * (((g - b) / d) % 6);
        else if (max == g) h = 60 * ((b - r) / d + 2);
        else h = 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
    }

    static Color HsvToRgb(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c, r, g, b;
        if (h < 60) (r, g, b) = (c, x, 0);
        else if (h < 120) (r, g, b) = (x, c, 0);
        else if (h < 180) (r, g, b) = (0, c, x);
        else if (h < 240) (r, g, b) = (0, x, c);
        else if (h < 300) (r, g, b) = (x, 0, c);
        else (r, g, b) = (c, 0, x);
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    // Warna dominan cover (ala Spotify): bin hue, bobot = saturasi x kecerahan
    static Color Dominant(BitmapSource src)
    {
        try
        {
            var small = new TransformedBitmap(src, new ScaleTransform(32.0 / src.PixelWidth, 32.0 / src.PixelHeight));
            var fc = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
            int pw = fc.PixelWidth, ph = fc.PixelHeight, stride = pw * 4;
            var px = new byte[ph * stride];
            fc.CopyPixels(px, stride, 0);

            var wt = new double[12]; var sr = new double[12]; var sg = new double[12]; var sb = new double[12];
            double ar = 0, ag = 0, ab = 0; int n = 0;
            for (int i = 0; i < px.Length; i += 4)
            {
                double b = px[i] / 255.0, g = px[i + 1] / 255.0, r = px[i + 2] / 255.0;
                ar += r; ag += g; ab += b; n++;
                RgbToHsv(r, g, b, out var hh, out var s, out var v);
                if (v < 0.18 || s < 0.2) continue;
                double k = s * v;
                int bin = (int)(hh / 30) % 12;
                wt[bin] += k; sr[bin] += r * k; sg[bin] += g * k; sb[bin] += b * k;
            }
            if (n == 0) return BlueGlow;
            int best = 0;
            for (int i = 1; i < 12; i++) if (wt[i] > wt[best]) best = i;

            if (wt[best] < n * 0.02)   // cover hampir abu-abu / hitam putih
            {
                RgbToHsv(ar / n, ag / n, ab / n, out var gh, out _, out var gv);
                return HsvToRgb(gh, 0.18, Math.Clamp(gv, 0.5, 0.72));
            }
            RgbToHsv(sr[best] / wt[best], sg[best] / wt[best], sb[best] / wt[best], out var dh, out var ds, out var dv);
            return HsvToRgb(dh, Math.Clamp(ds * 1.05, 0.55, 0.95), Math.Clamp(dv, 0.6, 0.86));
        }
        catch { return BlueGlow; }
    }

    // ===================== Bentuk & mode =====================
    static Geometry Shape(double w, double h)
    {
        double e = 16, r = Math.Min(22, h - e - 2);
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(0, 0), true, true);
            c.LineTo(new Point(w, 0), true, false);
            c.QuadraticBezierTo(new Point(w - e, 0), new Point(w - e, e), true, false);
            c.LineTo(new Point(w - e, h - r), true, false);
            c.QuadraticBezierTo(new Point(w - e, h), new Point(w - e - r, h), true, false);
            c.LineTo(new Point(e + r, h), true, false);
            c.QuadraticBezierTo(new Point(e, h), new Point(e, h - r), true, false);
            c.LineTo(new Point(e, e), true, false);
            c.QuadraticBezierTo(new Point(e, 0), new Point(0, 0), true, false);
        }
        g.Freeze();
        return g;
    }

    void Island_SizeChanged(object s, SizeChangedEventArgs e)
    {
        var g = Shape(e.NewSize.Width, e.NewSize.Height);
        Bg.Data = g; Island.Clip = g;
    }

    void SetSize()
    {
        tw = lyricsMode ? LW : EW;
        th = lyricsMode ? LH : PageH[page];
    }

    void Expand()
    {
        expanded = true; happy = false;
        if (lyricPinned && page == 0) { lyricsMode = true; LyricsCol.Visibility = Visibility.Visible; }
        SetSize();
    }

    void Collapse()
    {
        lyricPinned = lyricsMode;            // tetap tampilkan lirik (versi ramping) setelah menutup
        expanded = false; lyricsMode = false;
        LyricsCol.Visibility = Visibility.Collapsed;
    }

    async void Island_MouseEnter(object s, System.Windows.Input.MouseEventArgs e)
    {
        if (expanded) return;
        happy = true;
        await Task.Delay(350);
        if (Island.IsMouseOver && !expanded) Expand();
    }
    async void Island_MouseLeave(object s, System.Windows.Input.MouseEventArgs e)
    {
        happy = false;
        await Task.Delay(180);
        if (expanded && !Island.IsMouseOver && !Island.IsKeyboardFocusWithin) Collapse();
    }
    async void Island_LostFocus(object s, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        await Task.Delay(300);
        if (expanded && !Island.IsMouseOver && !Island.IsKeyboardFocusWithin) Collapse();
    }
    void Island_Click(object s, System.Windows.Input.MouseButtonEventArgs e) { if (!expanded) Expand(); }
    void Exit_Click(object s, RoutedEventArgs e) => Application.Current.Shutdown();

    void Lyrics_Click(object s, RoutedEventArgs e)
    {
        lyricsMode = !lyricsMode;
        LyricsCol.Visibility = lyricsMode ? Visibility.Visible : Visibility.Collapsed;
        SetSize();
    }

    void PagePrev_Click(object s, RoutedEventArgs e) => SetPage(Order[(Pos(page) + PageCount - 1) % PageCount], -1);
    void PageNext_Click(object s, RoutedEventArgs e) => SetPage(Order[(Pos(page) + 1) % PageCount], 1);

    void Settings_Click(object s, RoutedEventArgs e)
    {
        if (settingsWin == null)
        {
            settingsWin = new SettingsWindow();
            settingsWin.Closed += (_, _) => settingsWin = null;
            settingsWin.Show();
        }
        else
        {
            if (settingsWin.WindowState == WindowState.Minimized) settingsWin.WindowState = WindowState.Normal;
            settingsWin.Activate();
        }
    }

    // Geser + fade halaman baru masuk (dir: 1 = dari kanan, -1 = dari kiri)
    static void SlideIn(FrameworkElement el, int dir)
    {
        if (el.RenderTransform is not TranslateTransform tt) { tt = new TranslateTransform(); el.RenderTransform = tt; }
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dir * 40, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease });
        el.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)));
    }

    // ===================== Loop per frame =====================
    void Notify(string icon, string title, string sub, string hex, double secs = 4, string cat = "")
    {
        if (cat != "" && !Prefs.Current.Allow(cat)) return;     // dimatikan di Pengaturan
        Dispatcher.Invoke(() =>
        {
            ToastIcon.Text = icon; ToastTitle.Text = title; ToastSub.Text = sub;
            ToastSub.Visibility = sub == "" ? Visibility.Collapsed : Visibility.Visible;
            ToastBadge.Background = (Brush)new BrushConverter().ConvertFromString(hex)!;
            toastUntil = sw.Elapsed.TotalSeconds + secs;
        });
    }

    static void Fade(UIElement el, double op)
    {
        el.Opacity = op;
        el.Visibility = op > 0.01 ? Visibility.Visible : Visibility.Collapsed;
    }

    void Frame()
    {
        double now = sw.Elapsed.TotalSeconds, dt = Math.Min(now - last, 0.033);
        last = now;
        if (dt <= 0) return;

        var ps = Prefs.Current;

        // mode saat island tertutup: HUD (volume/caps) > notifikasi > lirik > fokus > terminal > mata
        string mode = "eyes";
        if (!expanded)
        {
            if (now < hudUntil) mode = "hud";
            else if (now < toastUntil) mode = "toast";
            else if (lyricPinned && playing && lyrics != null) mode = "lyric";
            else if (focusRunning && ps.NotifFocus) mode = "focus";
            else if (termName != null) mode = "term";
            (tw, th) = mode switch
            {
                "hud" => (350.0, 56.0),
                "toast" => (380.0, 56.0),
                "lyric" => (470.0, 46.0),
                "focus" => (268.0, CH),
                "term" => (270.0, CH),
                _ => (CW, CH)
            };
        }

        // warna: lagu (cover) saat halaman musik / lirik, selain itu biru menyala
        bool songMode = hasArt && ps.SongColor && (expanded ? page == 0 : mode == "lyric");
        Color t1 = songMode ? songBot : userC1;
        Color t2 = songMode ? songBot : (ps.TwoColors ? userC2 : userC1);
        bool two = ps.TwoColors && !songMode;
        double kc = 1 - Math.Exp(-5 * dt);
        curBot = Ease(curBot, t1, kc); curBot2 = Ease(curBot2, t2, kc);
        if (ColorDiff(curBot, appliedBot) + ColorDiff(curBot2, appliedBot2) >= 1 || ps.GradientEnabled != appliedGrad || two != appliedTwo)
            ApplyTheme(curBot, curBot2, two);

        // pegas ukuran
        const double k = 240, c = 24;
        vw += ((tw - w) * k - vw * c) * dt; w += vw * dt;
        vh += ((th - h) * k - vh * c) * dt; h += vh * dt;
        double nw = Math.Max(w, 80), nh = Math.Max(h, 30);
        if (Math.Abs(nw - Holder.Width) > 0.05) Holder.Width = nw;
        if (Math.Abs(nh - Holder.Height) > 0.05) Holder.Height = nh;

        // fade lapisan
        double f = 1 - Math.Exp(-14 * dt);
        ex += ((expanded ? 1 : 0) - ex) * (1 - Math.Exp(-12 * dt));
        opEyes += ((mode == "eyes" && !expanded && ps.Bot ? 1 : 0) - opEyes) * f;
        opHud += ((mode == "hud" ? 1 : 0) - opHud) * f;
        opLyric += ((mode == "lyric" ? 1 : 0) - opLyric) * f;
        opToast += ((mode == "toast" ? 1 : 0) - opToast) * f;
        opTerm += ((mode == "term" ? 1 : 0) - opTerm) * f;
        opFocus += ((mode == "focus" ? 1 : 0) - opFocus) * f;
        Fade(Panel, ex); Fade(ClockBox, ex); Fade(HeadRight, ex); Fade(Tabs, ex); Fade(PageTitle, ex);
        Fade(Bot, opEyes); Fade(SlimLyric, opLyric); Fade(ToastPanel, opToast); Fade(TermPanel, opTerm); Fade(FocusPanel, opFocus); Fade(HudPanel, opHud);
        if (opHud > 0.01)
        {
            hudShown += (hudTarget - hudShown) * (1 - Math.Exp(-16 * dt));
            HudBar.Width = 172 * Math.Clamp(hudShown, 0, 1);
        }

        double t = Environment.TickCount64 / 1000.0;
        if (ex > 0.01)
            for (int i = 0; i < 5; i++)
                bars[i].Height = playing ? 5 + 15 * Math.Abs(Math.Sin(t * (3 + i * 0.9) + i)) : 4;
        if (opTerm > 0.01)
        {
            D1.Opacity = 0.25 + 0.75 * Math.Max(0, Math.Sin(t * 6));
            D2.Opacity = 0.25 + 0.75 * Math.Max(0, Math.Sin(t * 6 - 0.9));
            D3.Opacity = 0.25 + 0.75 * Math.Max(0, Math.Sin(t * 6 - 1.8));
        }
        BotTick(now, dt, Native.Cursor(this));
    }

    // ===================== Bot: mata + topi Natal =====================
    void BotTick(double now, double dt, Point cur)
    {
        bool down = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0;
        double idle = now - lastMove;
        if (Math.Abs(cur.X - lastCur.X) + Math.Abs(cur.Y - lastCur.Y) > 1.5 || down || expanded)
        {
            if (idle > 4 && !expanded)   // bangun kaget!
            {
                wakeUntil = now + 1.4;
                hatVel += idle > 17 ? 260 : 140;
                hatLiftV = idle > 17 ? -150 : -90;
            }
            lastMove = now; lastCur = cur; idle = 0;
        }
        if (opEyes < 0.02) return;   // tidak terlihat -> hemat CPU

        string st = now < wakeUntil ? "wake"
                  : happy ? "happy"
                  : idle > 4 && playing ? "groove"
                  : idle > 17 ? "sleep"
                  : idle > 12 ? "sleepy"
                  : idle > 4 ? "coffee" : "awake";

        double dxm = cur.X - (Left + Width / 2), dym = cur.Y - (Top + h / 2);
        double lookX = Math.Clamp(dxm / 45, -1, 1) * 7, lookY = Math.Clamp(dym / 45, -1, 1) * 3;
        double lx = lookX, ly = lookY, sy = 1, sx = 1;
        Brush col = Cyan;
        bool showCup = false, showZ = false, showNotes = false, bang = false, sip = false;

        switch (st)
        {
            case "happy": sy = 0.45; col = Pink; break;
            case "wake": sy = 1.3; sx = 1.12; bang = true; break;
            case "coffee":
                lx = -9; ly = 2; col = Warm; showCup = true;
                sip = (idle - 4) % 3.6 > 2.4;
                sy = blink ? 0.08 : sip ? 0.3 : 0.85;
                break;
            case "groove":
                lx = Math.Sin(now * 3) * 4; ly = -Math.Abs(Math.Sin(now * 6)) * 2.5;
                sy = 0.7 + 0.3 * Math.Abs(Math.Sin(now * 6)); col = Pink; showNotes = true;
                break;
            case "sleepy":
                lx = 0; ly = 2 + Math.Sin(now * 1.2); col = Drowsy;
                sy = 0.28 + 0.22 * (0.5 + 0.5 * Math.Sin(now));
                break;
            case "sleep":
                lx = 0; ly = 3 + Math.Sin(now * 1.6) * 0.8; sy = 0.07; col = Drowsy; showZ = true;
                break;
            default: sy = blink ? 0.08 : 1; break;
        }

        // mata
        double a = 1 - Math.Exp(-14 * dt), kk = 1 - Math.Exp(-30 * dt);
        Look.X += (lx - Look.X) * a; Look.Y += (ly - Look.Y) * a;
        ScL.ScaleY += (sy - ScL.ScaleY) * kk; ScR.ScaleY = ScL.ScaleY;
        ScL.ScaleX += (sx - ScL.ScaleX) * kk; ScR.ScaleX = ScL.ScaleX;
        if (!ReferenceEquals(col, lastCol)) { EyeL.Background = col; EyeR.Background = col; lastCol = col; }

        // topi: ayunan pegas + kelembaman saat wajah bergerak, pom-pom berayun
        bool droop = st is "sleep" or "sleepy";
        double dLook = Look.X - lookPrevX; lookPrevX = Look.X;
        double hatTarget = droop ? 18 : Look.X * 2.2;
        hatVel += (-(hatAng - hatTarget) * 110 - hatVel * 10) * dt - dLook * 90;
        hatAng = Math.Clamp(hatAng + hatVel * dt, -40, 40);
        double pomTarget = droop ? 30 : -hatAng * 0.7;
        pomVel += (-(pomAng - pomTarget) * 70 - pomVel * 5) * dt - dLook * 120;
        pomAng = Math.Clamp(pomAng + pomVel * dt, -55, 55);
        double liftTarget = st == "happy" ? -2 * Math.Abs(Math.Sin(now * 9))
                          : st == "groove" ? -1.5 * Math.Abs(Math.Sin(now * 6))
                          : st == "sleep" ? 2 : 0;
        hatLiftV += (-(hatLift - liftTarget) * 220 - hatLiftV * 14) * dt;
        hatLift += hatLiftV * dt;
        HatRot.Angle = hatAng; PomRot.Angle = pomAng; HatTr.Y = hatLift;

        // properti: kopi
        cupOp += ((showCup ? 1 : 0) - cupOp) * (1 - Math.Exp(-8 * dt));
        Cup.Opacity = cupOp;
        if (cupOp > 0.02)
        {
            cupAng += ((sip ? -24 : 0) - cupAng) * (1 - Math.Exp(-9 * dt));
            CupTilt.Angle = cupAng;
            var steam = new[] { S1, S2, S3 };
            for (int i = 0; i < 3; i++)
            {
                steam[i].Opacity = 0.2 + 0.6 * Math.Abs(Math.Sin(now * 1.7 + i * 1.3));
                Canvas.SetLeft(steam[i], 7 + i * 5 + Math.Sin(now * 2.2 + i) * 1.3);
                Canvas.SetTop(steam[i], 3 - ((now * 5 + i * 2.5) % 5) * 0.5);
            }
        }
        // zzz
        zOp += ((showZ ? 1 : 0) - zOp) * (1 - Math.Exp(-6 * dt));
        if (zOp > 0.02)
        {
            var zs = new[] { Z1, Z2, Z3 };
            for (int i = 0; i < 3; i++)
            {
                double ph = (now * 0.45 + i / 3.0) % 1;
                zs[i].Opacity = zOp * Math.Sin(ph * Math.PI);
                Canvas.SetLeft(zs[i], 88 + ph * 14 + i * 1.5); Canvas.SetTop(zs[i], 24 - ph * 20);
            }
        }
        // not musik
        nOp += ((showNotes ? 1 : 0) - nOp) * (1 - Math.Exp(-6 * dt));
        if (nOp > 0.02)
        {
            double p1 = (now * 0.8) % 1, p2 = (now * 0.8 + 0.5) % 1;
            N1.Opacity = nOp * Math.Sin(p1 * Math.PI); Canvas.SetLeft(N1, 14 - p1 * 8); Canvas.SetTop(N1, 26 - p1 * 20);
            N2.Opacity = nOp * Math.Sin(p2 * Math.PI); Canvas.SetLeft(N2, 100 + p2 * 8); Canvas.SetTop(N2, 26 - p2 * 20);
        }
        Bang.Opacity = bang ? 1 : 0;
    }

    void SecondTick()
    {
        DateText.Text = DateTime.Now.ToString("ddd d MMM yyyy   HH:mm");
        UpdateTimePhase();
        if (termName != null)
        {
            var el = DateTime.Now - termStart;
            TermText.Text = $"{termName}  {(el.TotalHours >= 1 ? el.ToString(@"h\:mm\:ss") : el.ToString(@"mm\:ss"))}";
        }
        PrayerTick();
        if (focusRunning)
        {
            focusLeft--;
            if (focusLeft <= 0) FocusFinish();
            UpdateFocusUI();
        }
        CalendarTick();
        SysTick();
        if (++wxTick % 1200 == 0) _ = LoadWeather();
        PowerCheck();
    }

    void XmasGreeting()
    {
        var today = DateTime.Today; var xmas = new DateTime(today.Year, 12, 25);
        if (today > xmas) xmas = xmas.AddYears(1);
        int d = (xmas - today).Days;
        if (d == 0) Notify("🎅", "Selamat Natal!", "Semoga hari ini penuh sukacita", "#C23B3B", 6, "xmas");
        else Notify("🎄", $"{d} hari lagi menuju Natal", "Selamat datang di NexoIsland ❄", "#C23B3B", 5, "xmas");
    }

    void PowerCheck()
    {
        var (has, ac, pct) = Native.Power();
        if (!has) return;
        if (lastAc != null && ac != lastAc)
            Notify(ac ? "🔌" : "🔋", ac ? "Mengisi daya" : "Pengisi daya dicabut", pct + "%", ac ? "#2BB673" : "#6B7A99", 4, "battery");
        lastAc = ac;
        if (!ac && pct <= 15 && !lowWarned) { lowWarned = true; Notify("🪫", "Baterai lemah", pct + "% tersisa", "#E5484D", 6, "battery"); }
        if (ac || pct > 20) lowWarned = false;
    }

    // ===================== Musik & lirik =====================
    static string Fmt(double s) => TimeSpan.FromSeconds(Math.Max(0, s)).ToString(@"mm\:ss");

    void TickProgress()
    {
        if (!expanded && !lyricPinned) return;
        try
        {
            var s = mgr?.GetCurrentSession();
            if (s == null) return;
            var tl = s.GetTimelineProperties();
            double pos = tl.Position.TotalSeconds;
            if (playing) pos += (DateTimeOffset.Now - tl.LastUpdatedTime).TotalSeconds;
            double dur = (tl.EndTime - tl.StartTime).TotalSeconds;
            if (dur > 0) pos = Math.Min(pos, dur);

            int idx = -1;
            if (lyrics != null)
                for (int i = 0; i < lyrics.Count; i++) { if (lyrics[i].t <= pos + 0.25) idx = i; else break; }
            SlimLyric.Text = idx >= 0 && lyrics![idx].l != "" ? lyrics[idx].l : "♪";

            if (!expanded) return;
            Fill.Width = dur > 0 ? Track.ActualWidth * pos / dur : 0;
            PosText.Text = Fmt(pos);
            RemText.Text = dur > 0 ? "-" + Fmt(dur - pos) : "";

            if (!lyricsMode) return;
            if (lyrics == null) { L1.Text = ""; L2.Text = lyricState; L3.Text = ""; return; }
            L1.Text = idx > 0 ? lyrics[idx - 1].l : "";
            L2.Text = idx >= 0 ? lyrics[idx].l : "";
            L3.Text = idx + 1 < lyrics.Count ? lyrics[idx + 1].l : "";
        }
        catch { }
    }

    async Task Refresh()
    {
        try
        {
            var session = mgr?.GetCurrentSession();
            if (session == null)
            {
                TitleText.Text = "Tidak ada musik"; ArtistText.Text = "";
                playing = false; PlayBtn.Content = "▶"; lyrics = null; lyricState = ""; lastKey = "";
                ArtBrush.ImageSource = null; hasArt = false;
                return;
            }
            playing = session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            PlayBtn.Content = playing ? "⏸" : "▶";

            var p = await session.TryGetMediaPropertiesAsync();
            TitleText.Text = string.IsNullOrEmpty(p.Title) ? "Tidak ada musik" : p.Title;
            ArtistText.Text = p.Artist;

            string key = p.Artist + "|" + p.Title;
            if (key != lastKey || ArtBrush.ImageSource == null) await LoadArt(p);
            if (key != lastKey && !string.IsNullOrEmpty(p.Title))
            {
                lastKey = key;
                _ = LoadLyrics(key, p.Artist, p.Title);
            }
        }
        catch { }
    }

    async Task LoadArt(GlobalSystemMediaTransportControlsSessionMediaProperties p)
    {
        try
        {
            if (p.Thumbnail == null) { ArtBrush.ImageSource = null; hasArt = false; return; }
            using var rs = await p.Thumbnail.OpenReadAsync();
            var ms = new MemoryStream();
            await rs.AsStream().CopyToAsync(ms);
            ms.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.StreamSource = ms; bi.EndInit(); bi.Freeze();
            ArtBrush.ImageSource = bi;
            songBot = Dominant(bi); hasArt = true;     // warna island mengikuti cover lagu
        }
        catch { ArtBrush.ImageSource = null; hasArt = false; }
    }

    async Task LoadLyrics(string key, string artist, string title)
    {
        lyrics = null; lyricState = "Mencari lirik…";
        try
        {
            string url = "https://lrclib.net/api/search?track_name=" + Uri.EscapeDataString(title)
                       + "&artist_name=" + Uri.EscapeDataString(artist);
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url));
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.TryGetProperty("syncedLyrics", out var sl) && sl.ValueKind == JsonValueKind.String)
                {
                    var list = new List<(double t, string l)>();
                    foreach (Match m in Regex.Matches(sl.GetString()!, @"\[(\d+):(\d+(?:\.\d+)?)\](.*)"))
                        list.Add((int.Parse(m.Groups[1].Value) * 60 + double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                                  m.Groups[3].Value.Trim()));
                    if (list.Count > 0) { if (key == lastKey) lyrics = list; return; }
                }
            }
            if (key == lastKey) lyricState = "Lirik tidak ditemukan";
        }
        catch { if (key == lastKey) lyricState = "Lirik tidak tersedia"; }
    }

    async void Prev_Click(object s, RoutedEventArgs e) { try { await mgr!.GetCurrentSession().TrySkipPreviousAsync(); await Task.Delay(400); await Refresh(); } catch { } }
    async void Play_Click(object s, RoutedEventArgs e) { try { await mgr!.GetCurrentSession().TryTogglePlayPauseAsync(); await Task.Delay(300); await Refresh(); } catch { } }
    async void Next_Click(object s, RoutedEventArgs e) { try { await mgr!.GetCurrentSession().TrySkipNextAsync(); await Task.Delay(400); await Refresh(); } catch { } }

    // ===================== Jadwal shalat (sun arc + kartu) =====================
    static void PlaceOnArc(FrameworkElement e, double f, double half)
    {
        double th = Math.PI * (1 - Math.Clamp(f, 0, 1));
        Canvas.SetLeft(e, Cx + R * Math.Cos(th) - half);
        Canvas.SetTop(e, Cy - R * Math.Sin(th) - half);
    }

    void BuildArc()
    {
        var track = new WPath
        {
            Stroke = new SolidColorBrush(Color.FromArgb(0x4A, 0xBF, 0xE9, 0xFF)), StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 2, 3 },
            Data = new PathGeometry(new[] { new PathFigure(new Point(Cx - R, Cy),
                       new PathSegment[] { new ArcSegment(new Point(Cx + R, Cy), new Size(R, R), 0, false, SweepDirection.Clockwise, true) }, false) })
        };
        var horizon = new Line { X1 = 4, X2 = 216, Y1 = Cy + 5, Y2 = Cy + 5, Stroke = new SolidColorBrush(Color.FromArgb(0x30, 255, 255, 255)), StrokeThickness = 1 };
        arcDone = new WPath { Stroke = accentBr, StrokeThickness = 3, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        sunGlow = new Ellipse
        {
            Width = 34, Height = 34,
            Fill = new RadialGradientBrush(Color.FromArgb(0xA0, 0xFF, 0xD2, 0x7A), Color.FromArgb(0x00, 0xFF, 0xD2, 0x7A))
        };
        sun = new Ellipse
        {
            Width = 15, Height = 15,
            Fill = new RadialGradientBrush(Color.FromRgb(0xFF, 0xF3, 0xC4), Color.FromRgb(0xFF, 0xB8, 0x3D))
        };
        moon = new TextBlock { Text = "🌙", FontFamily = new FontFamily("Segoe UI Emoji"), FontSize = 20, Visibility = Visibility.Collapsed };

        ArcCanvas.Children.Add(horizon); ArcCanvas.Children.Add(track); ArcCanvas.Children.Add(arcDone);
        for (int i = 0; i < 5; i++)
        {
            pDots[i] = new Ellipse { Width = 7, Height = 7, Stroke = accentBr, StrokeThickness = 1.5, Fill = Brushes.Transparent, Visibility = Visibility.Collapsed };
            ArcCanvas.Children.Add(pDots[i]);
        }
        ArcCanvas.Children.Add(sunGlow); ArcCanvas.Children.Add(sun);
        Canvas.SetLeft(moon, Cx - 12); Canvas.SetTop(moon, Cy - R - 6);
        ArcCanvas.Children.Add(moon);
    }

    void BuildPrayerCards()
    {
        for (int i = 0; i < 5; i++)
        {
            PrayerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var ic = new TextBlock { Text = pIcons[i], FontFamily = new FontFamily("Segoe UI Emoji"), FontSize = 17, HorizontalAlignment = HorizontalAlignment.Center };
            pLabels[i] = new TextBlock { Text = pNames[i], FontSize = 11.5, Foreground = Dim, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
            pTimes[i] = new TextBlock { Text = "--:--", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
                                        HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(ic); sp.Children.Add(pLabels[i]); sp.Children.Add(pTimes[i]);
            pCards[i] = new Border { CornerRadius = new CornerRadius(14), Margin = new Thickness(i == 0 ? 0 : 4, 0, i == 4 ? 0 : 4, 0),
                                     Background = CardBg, BorderBrush = CardEdge, BorderThickness = new Thickness(1), Child = sp };
            Grid.SetColumn(pCards[i], i);
            PrayerGrid.Children.Add(pCards[i]);
        }
    }

    async Task LoadPrayers()
    {
        try
        {
            var pf = Prefs.Current;
            string url = $"https://api.aladhan.com/v1/timingsByCity?city={Uri.EscapeDataString(pf.City)}&country={Uri.EscapeDataString(pf.Country)}&method=20";
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url));
            var tm = doc.RootElement.GetProperty("data").GetProperty("timings");
            var d = new Dictionary<string, TimeSpan>();
            for (int i = 0; i < 5; i++)
            {
                d[pKeys[i]] = TimeSpan.Parse(tm.GetProperty(pKeys[i]).GetString()!.Substring(0, 5), CultureInfo.InvariantCulture);
                pTimes[i].Text = d[pKeys[i]].ToString(@"hh\:mm");
                pDots[i].Visibility = Visibility.Visible;
            }
            prayers = d; prayerDate = DateTime.Today;
            PrayerTick();
        }
        catch { PrayerNext.Text = "Jadwal tidak tersedia"; }
    }

    void UpdateArc(TimeSpan tod)
    {
        var fj = prayers!["Fajr"]; var ish = prayers["Isha"];
        double span = Math.Max(1, (ish - fj).TotalSeconds);
        double f = Math.Clamp((tod - fj).TotalSeconds / span, 0, 1);
        bool night = tod < fj || tod > ish;

        for (int i = 0; i < 5; i++)
        {
            var t = prayers[pKeys[i]];
            PlaceOnArc(pDots[i], (t - fj).TotalSeconds / span, 3.5);
            pDots[i].Fill = t <= tod ? accentBr : Brushes.Transparent;
        }

        double fd = tod > ish ? 1 : f;
        if (fd > 0.003)
        {
            double th = Math.PI * (1 - fd);
            var end = new Point(Cx + R * Math.Cos(th), Cy - R * Math.Sin(th));
            arcDone.Data = new PathGeometry(new[] { new PathFigure(new Point(Cx - R, Cy),
                               new PathSegment[] { new ArcSegment(end, new Size(R, R), 0, false, SweepDirection.Clockwise, true) }, false) });
        }
        else arcDone.Data = null;

        sun.Visibility = sunGlow.Visibility = night ? Visibility.Collapsed : Visibility.Visible;
        moon.Visibility = night ? Visibility.Visible : Visibility.Collapsed;
        if (!night) { PlaceOnArc(sun, f, 7.5); PlaceOnArc(sunGlow, f, 17); }
    }

    void PrayerTick()
    {
        if (prayers == null) return;
        if (DateTime.Today != prayerDate) { prayerDate = DateTime.Today; _ = LoadPrayers(); }
        var tod = DateTime.Now.TimeOfDay;
        int next = -1;
        for (int i = 0; i < 5; i++) if (prayers[pKeys[i]] > tod) { next = i; break; }
        bool tomorrow = next < 0;
        TimeSpan nextT = tomorrow ? prayers["Fajr"] + TimeSpan.FromHours(24) : prayers[pKeys[next]];
        TimeSpan prevT = tomorrow ? prayers["Isha"] : next > 0 ? prayers[pKeys[next - 1]] : prayers["Isha"] - TimeSpan.FromHours(24);
        TimeSpan remain = nextT - tod;
        if (tomorrow) next = 0;

        PrayerNext.Text = pNames[next];
        PrayerAt.Text = prayers[pKeys[next]].ToString(@"hh\:mm");
        PrayerCount.Text = "- " + remain.ToString(@"hh\:mm\:ss");
        double frac = 1 - remain.TotalSeconds / Math.Max(1, (nextT - prevT).TotalSeconds);
        if (PrayerBarTrack.ActualWidth > 0) PrayerBar.Width = PrayerBarTrack.ActualWidth * Math.Clamp(frac, 0, 1);

        for (int i = 0; i < 5; i++)
        {
            bool isNext = i == next, passed = prayers[pKeys[i]] <= tod && !(tomorrow && i == 0);
            pCards[i].Background = isNext ? CardHi : CardBg;
            pCards[i].BorderBrush = isNext ? CardHiEdge : CardEdge;
            pCards[i].Opacity = passed ? 0.5 : 1;
            pLabels[i].Foreground = isNext ? Brushes.White : Dim;
        }
        UpdateArc(tod);

        for (int i = 0; i < 5; i++)
        {
            var pt = prayers[pKeys[i]];
            string id = DateTime.Today.ToString("yyyyMMdd") + i;
            if (pt.Hours == tod.Hours && pt.Minutes == tod.Minutes && notified != id)
            {
                notified = id;
                Notify("🕌", "Waktu " + pNames[i], "Saatnya menunaikan shalat", "#C9962B", 9, "prayer");
            }
        }
    }

    // ===================== WiFi, Bluetooth, Terminal =====================
    static string? Ssid()
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", "wlan show interfaces") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEnd(); p.WaitForExit(2000);
            var m = Regex.Match(o, @"^\s*SSID\s*:\s*(.+?)\s*$", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value : null;
        }
        catch { return null; }
    }

    void StartWifi()
    {
        _ = Task.Run(() => lastSsid = Ssid());
        NetworkChange.NetworkAddressChanged += async (_, _) =>
        {
            int t = ++netTick;
            await Task.Delay(1800);
            if (t != netTick) return;
            var s = await Task.Run(Ssid);
            if (s == lastSsid) return;
            var prev = lastSsid; lastSsid = s;
            if (s != null) Notify("📶", "Terhubung ke WiFi", s, "#2E9BFF", 4, "wifi");
            else Notify("📵", "WiFi terputus", prev ?? "", "#6B7A99", 4, "wifi");
        };
    }

    void StartBluetooth()
    {
        try
        {
            foreach (var sel in new[]
            {
                BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected),
                BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected)
            })
            {
                var names = new Dictionary<string, string>();
                bool ready = false;
                var w = DeviceInformation.CreateWatcher(sel);
                w.Added += (_, d) => { names[d.Id] = d.Name; if (ready) Notify("🎧", "Bluetooth terhubung", d.Name, "#3B6CFF", 4, "bt"); };
                w.Removed += (_, u) => { if (names.Remove(u.Id, out var n) && ready) Notify("🔌", "Bluetooth terputus", n, "#6B7A99", 4, "bt"); };
                w.EnumerationCompleted += (_, _) => ready = true;
                w.Start();
            }
        }
        catch { }
    }

    void ScanTerminal()
    {
        try
        {
            var all = Process.GetProcesses();
            if (!Prefs.Current.NotifTerminal)     // dimatikan: jangan lacak proses, anggap semua proses yang ada sebagai dasar
            {
                termPid = -1; termName = null;
                baseline.UnionWith(all.Select(p => p.Id));
                return;
            }
            baseline.IntersectWith(all.Select(p => p.Id));
            if (termPid != -1)
            {
                if (all.Any(p => p.Id == termPid)) return;
                var el = DateTime.Now - termStart;
                Notify("✅", "Proses selesai", $"{termName} · {(el.TotalHours >= 1 ? el.ToString(@"h\:mm\:ss") : el.ToString(@"mm\:ss"))}", "#2BB673", 5, "term");
                termPid = -1; termName = null;
            }
            foreach (var p in all)
            {
                if (baseline.Contains(p.Id) || !Dev.Contains(p.ProcessName)) continue;
                var parent = Native.ParentName(p.Id);
                if (parent == null || !Shells.Contains(parent)) continue;
                termPid = p.Id; termName = p.ProcessName; termStart = DateTime.Now;
                break;
            }
        }
        catch { }
    }

    // ===================== Pengaturan (diterapkan langsung) =====================
    void ApplyPrefs()
    {
        var p = Prefs.Current;
        userC1 = p.C1; userC2 = p.C2;
        Halo.Visibility = p.Halo && p.GradientEnabled ? Visibility.Visible : Visibility.Collapsed;
        Hat.Visibility = p.Hat ? Visibility.Visible : Visibility.Collapsed;
        TimeIconCv.Visibility = PhaseText.Visibility = p.TimeIcon ? Visibility.Visible : Visibility.Collapsed;

        bool overlayOn = p.Snow || p.SnowCaps || p.XmasIcons;
        if (overlayOn != lastSnow)
        {
            lastSnow = overlayOn;   // salju baru dinyalakan -> pastikan island tetap di atasnya
            Dispatcher.InvokeAsync(() => { Topmost = false; Topmost = true; });
        }
        if (p.City != loadedCity || p.Country != loadedCountry)
        {
            bool first = loadedCity == "";
            loadedCity = p.City; loadedCountry = p.Country;
            PrayerCity.Text = "📍 " + p.City + ", " + p.Country;
            if (!first) { prayers = null; PrayerNext.Text = "Memuat…"; _ = LoadPrayers(); wxLat = null; _ = LoadWeather(); }
        }
    }

    // ===================== Ikon fase hari =====================
    static readonly string[] PhaseNames = { "Malam", "Pagi", "Pagi menuju siang", "Siang", "Siang menuju sore", "Sore", "Sore menuju malam" };

    static int PhaseOf(TimeSpan t)
    {
        double m = t.TotalMinutes;
        if (m < 4.5 * 60) return 0;      // 00.00 - 04.30  malam
        if (m < 8 * 60) return 1;        // 04.30 - 08.00  pagi
        if (m < 11 * 60) return 2;       // 08.00 - 11.00  pagi menuju siang
        if (m < 14.5 * 60) return 3;     // 11.00 - 14.30  siang
        if (m < 16.5 * 60) return 4;     // 14.30 - 16.30  siang menuju sore
        if (m < 18 * 60) return 5;       // 16.30 - 18.00  sore
        if (m < 19.5 * 60) return 6;     // 18.00 - 19.30  sore menuju malam
        return 0;                        // 19.30 - 24.00  malam
    }

    void UpdateTimePhase()
    {
        int ph = PhaseOf(DateTime.Now.TimeOfDay);
        if (ph == timePhase) return;
        timePhase = ph;
        PhaseText.Text = PhaseNames[ph];
        BuildTimeIcon(ph);
    }

    static Color Col(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    static Brush Solid(string hex) => new SolidColorBrush(Col(hex));
    static Brush Vert(string top, string bottom) => new LinearGradientBrush(Col(top), Col(bottom), 90);

    static Ellipse Dot(double cx, double cy, double r, Brush fill, double op = 1)
    {
        var e = new Ellipse { Width = r * 2, Height = r * 2, Fill = fill, Opacity = op };
        Canvas.SetLeft(e, cx - r); Canvas.SetTop(e, cy - r);
        return e;
    }

    static Line Seg(double x1, double y1, double x2, double y2, Brush b, double th = 1.6, double op = 1) =>
        new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = b, StrokeThickness = th, Opacity = op,
                   StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };

    static Line Ray(double cx, double cy, double deg, double r1, double r2, Brush b, double th = 1.8)
    {
        double a = deg * Math.PI / 180;
        return Seg(cx + r1 * Math.Cos(a), cy + r1 * Math.Sin(a), cx + r2 * Math.Cos(a), cy + r2 * Math.Sin(a), b, th);
    }

    // awan: kotak 16 x 10 yang diskala lalu digeser ke (x, y)
    static WPath Cloud(double x, double y, double s, Brush fill, double op = 1)
    {
        var g = new GeometryGroup { FillRule = FillRule.Nonzero };
        g.Children.Add(new EllipseGeometry(new Point(4.5, 6.5), 3.5, 3.5));
        g.Children.Add(new EllipseGeometry(new Point(9, 4.5), 4.5, 4.5));
        g.Children.Add(new EllipseGeometry(new Point(12.6, 6.9), 3.1, 3.1));
        g.Children.Add(new RectangleGeometry(new Rect(4.5, 6.5, 8.2, 3.5)));
        g.Transform = new TransformGroup { Children = { new ScaleTransform(s, s), new TranslateTransform(x, y) } };
        return new WPath { Data = g, Fill = fill, Opacity = op };
    }

    // bulan sabit
    static WPath Moon(double cx, double cy, double r, Brush fill)
    {
        var g = new CombinedGeometry(GeometryCombineMode.Exclude,
            new EllipseGeometry(new Point(cx, cy), r, r),
            new EllipseGeometry(new Point(cx + r * 0.62, cy - r * 0.42), r * 0.85, r * 0.85));
        return new WPath { Data = g, Fill = fill };
    }

    // setengah matahari di atas garis horizon (y)
    static WPath HalfSun(double cx, double y, double r, Brush fill) =>
        new WPath { Data = Geometry.Parse(FormattableString.Invariant($"M{cx - r},{y} A{r},{r} 0 0 1 {cx + r},{y} Z")), Fill = fill };

    static void AddSun(Canvas cv, double cx, double cy, double r, string edge, string center, int rays, double r1, double r2)
    {
        for (int i = 0; i < rays; i++) cv.Children.Add(Ray(cx, cy, i * 360.0 / rays, r1, r2, Solid(edge)));
        cv.Children.Add(Dot(cx, cy, r, new RadialGradientBrush(Col(center), Col(edge))));
    }

    void BuildTimeIcon(int ph)
    {
        var cv = TimeIconCv; cv.Children.Clear();
        Brush white = Brushes.White;
        switch (ph)
        {
            case 0:   // malam: bulan + bintang
                cv.Children.Add(Moon(13, 14.5, 8.5, Vert("#FFF6D0", "#FFD27A")));
                cv.Children.Add(Dot(22.5, 6.5, 1.3, white, 0.9));
                cv.Children.Add(Dot(24.5, 13.5, 0.9, white, 0.7));
                cv.Children.Add(Dot(18.5, 3.8, 0.9, white, 0.7));
                break;
            case 1:   // pagi: matahari terbit di horizon
                for (double a = -160; a <= -20; a += 35) cv.Children.Add(Ray(14, 19.5, a, 8.6, 11.8, Solid("#FFC15A")));
                cv.Children.Add(HalfSun(14, 19.5, 6, Vert("#FFE08A", "#FF9F43")));
                cv.Children.Add(Seg(3, 19.5, 25, 19.5, white, 1.6, 0.75));
                cv.Children.Add(Seg(8, 22.6, 20, 22.6, white, 1.4, 0.4));
                cv.Children.Add(Seg(11, 25.4, 17, 25.4, white, 1.4, 0.25));
                break;
            case 2:   // pagi menuju siang: matahari mulai naik, awan tipis
                AddSun(cv, 11.5, 11.5, 5, "#FFC83D", "#FFF3C4", 8, 7.6, 10.6);
                cv.Children.Add(Cloud(11.5, 15, 0.95, Solid("#EAF3FF"), 0.97));
                break;
            case 3:   // siang: matahari terik
                cv.Children.Add(Dot(14, 14, 13, new RadialGradientBrush(Color.FromArgb(0x70, 0xFF, 0xD8, 0x4D), Color.FromArgb(0x00, 0xFF, 0xD8, 0x4D))));
                AddSun(cv, 14, 14, 6, "#FFB62E", "#FFF7C9", 8, 9.3, 12.6);
                break;
            case 4:   // siang menuju sore: matahari oranye + awan hangat
                AddSun(cv, 10.5, 11, 5, "#FF9F43", "#FFE29A", 8, 7.6, 10.4);
                cv.Children.Add(Cloud(10.5, 14, 1.05, Solid("#FFE9D2"), 0.97));
                break;
            case 5:   // sore: matahari terbenam
                for (double a = -155; a <= -25; a += 65) cv.Children.Add(Ray(14, 19.5, a, 9.5, 12.5, Solid("#FF9A6B")));
                cv.Children.Add(HalfSun(14, 19.5, 7.3, Vert("#FFB36B", "#FF4D6D")));
                cv.Children.Add(Seg(3, 19.5, 25, 19.5, white, 1.6, 0.75));
                cv.Children.Add(Seg(8, 22.6, 20, 22.6, Solid("#FFB3C7"), 1.4, 0.5));
                cv.Children.Add(Seg(11, 25.4, 17, 25.4, Solid("#FFB3C7"), 1.4, 0.3));
                break;
            default:  // sore menuju malam: matahari hampir tenggelam + bulan muncul
                cv.Children.Add(HalfSun(11.5, 20, 5.2, Vert("#D68BFF", "#7B5BFF")));
                cv.Children.Add(Seg(3, 20, 25, 20, white, 1.6, 0.7));
                cv.Children.Add(Seg(7, 23, 17, 23, Solid("#B9A6FF"), 1.4, 0.4));
                cv.Children.Add(Moon(21, 8, 4.6, Solid("#FFF0B8")));
                cv.Children.Add(Dot(6.5, 7, 0.9, white, 0.8));
                cv.Children.Add(Dot(15, 4.5, 0.8, white, 0.6));
                break;
        }
    }

    // ===================== HUD: Caps Lock, Num Lock, volume, mute =====================
    void Hud(string kind, string title, string sub, string value, double frac, string hex, double secs)
    {
        SetHudIcon(kind);
        HudTitle.Text = title; HudValue.Text = value;
        HudSub.Text = sub; HudSub.Visibility = sub == "" ? Visibility.Collapsed : Visibility.Visible;
        HudBarHost.Visibility = frac >= 0 ? Visibility.Visible : Visibility.Collapsed;
        HudBadge.Background = Solid(hex);
        if (frac >= 0) { hudTarget = frac; if (opHud < 0.05) hudShown = frac; }
        hudUntil = sw.Elapsed.TotalSeconds + secs;
    }

    void SetHudIcon(string kind)
    {
        HudIcon.Children.Clear();
        WPath P(string d, bool fill) => new WPath
        {
            Data = Geometry.Parse(d), Fill = fill ? Brushes.White : null, Stroke = fill ? null : Brushes.White,
            StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
        };
        switch (kind)
        {
            case "vol0": case "vol1": case "vol2": case "mute":
                HudIcon.Children.Add(P("M2,7.5 L6,7.5 L11,3 L11,17 L6,12.5 L2,12.5 Z", true));
                if (kind == "vol1" || kind == "vol2") HudIcon.Children.Add(P("M13.5,7 A4,4 0 0 1 13.5,13", false));
                if (kind == "vol2") HudIcon.Children.Add(P("M15.8,4.2 A7.5,7.5 0 0 1 15.8,15.8", false));
                if (kind == "mute") HudIcon.Children.Add(P("M14,7.5 L19,12.5 M19,7.5 L14,12.5", false));
                break;
            case "capsOn": case "capsOff":
                HudIcon.Children.Add(P("M10,2.5 L17,10.5 L12.6,10.5 L12.6,14 L7.4,14 L7.4,10.5 L3,10.5 Z", kind == "capsOn"));
                HudIcon.Children.Add(P("M7.4,17.4 L12.6,17.4", false));
                break;
            default:  // numOn / numOff
                var t = new TextBlock { Text = "123", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Opacity = kind == "numOn" ? 1 : 0.55 };
                Canvas.SetLeft(t, 0); Canvas.SetTop(t, 3);
                HudIcon.Children.Add(t);
                break;
        }
    }

    void KeysAndVolumeCheck()
    {
        var ps = Prefs.Current;

        // Caps Lock & Num Lock
        bool caps = (Native.GetKeyState(0x14) & 1) != 0, num = (Native.GetKeyState(0x90) & 1) != 0;
        if (lastCaps != null && caps != lastCaps && ps.Allow("caps"))
            Hud(caps ? "capsOn" : "capsOff", "Caps Lock", caps ? "Huruf kapital aktif" : "Huruf kapital mati", caps ? "ON" : "OFF", -1, caps ? "#D9A21B" : "#5E6B85", 1.8);
        lastCaps = caps;
        if (lastNum != null && num != lastNum && ps.Allow("caps"))
            Hud(num ? "numOn" : "numOff", "Num Lock", num ? "Tombol angka aktif" : "Tombol angka mati", num ? "ON" : "OFF", -1, num ? "#2BB673" : "#5E6B85", 1.8);
        lastNum = num;

        // Volume & mute
        var (ok, vol, mute) = Native.Volume();
        if (!ok) return;
        if (lastVol != null && lastMute != null && ps.Allow("vol"))
        {
            if (mute != lastMute)
            {
                if (mute) Hud("mute", "Suara dimatikan", "", "MUTE", 0, "#E5484D", 1.8);
                else Hud(VolIcon(vol), "Suara aktif", "", Math.Round(vol * 100) + "%", vol, "#2E7BFF", 1.8);
            }
            else if (Math.Abs(vol - lastVol.Value) >= 0.004f)
            {
                if (mute) Hud("mute", "Suara dimatikan", "", "MUTE", 0, "#E5484D", 1.8);
                else Hud(VolIcon(vol), vol > lastVol.Value ? "Volume naik" : "Volume turun", "", Math.Round(vol * 100) + "%", vol, "#2E7BFF", 1.6);
            }
        }
        lastVol = vol; lastMute = mute;
    }

    static string VolIcon(float v) => v < 0.01f ? "vol0" : v < 0.5f ? "vol1" : "vol2";
}
