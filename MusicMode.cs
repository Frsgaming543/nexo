using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using WPath = System.Windows.Shapes.Path;
namespace NexoIsland;

// MODE MUSIK (layar penuh) - terinspirasi setup "Verci + Lumn":
//  - layar gelap, cahaya menyala di tepi layar mengikuti warna cover dan berdenyut mengikuti level suara asli
//  - lirik besar bergerak (baris aktif besar + terisi pelan sesuai waktu, baris lain meredup & bergeser)
//  - tanpa lirik (atau tombol 💬/🖼) -> tampilan cover besar + judul + artis
//  - panel pemutar melayang: cover kecil, judul, ⏮ ⏯ ⏭, bar progres (klik = pindah posisi)
//  Tombol: Esc = tutup | Spasi = putar/jeda | ← → = lagu sebelumnya/berikutnya | C = ganti tampilan
//  Pengembang: class_programmer/farez
public partial class MusicModeWindow : Window
{
    readonly MainWindow main;
    readonly double sc, W, H;
    readonly Stopwatch clk = Stopwatch.StartNew();

    // cahaya
    readonly GradientStop washA = new(Colors.Transparent, 0), washB = new(Colors.Transparent, 1);
    readonly GradientStop glowA = new(Colors.Transparent, 0), glowB = new(Colors.Transparent, 1);
    class GlowLayer { public WPath P = null!; public SolidColorBrush Br = null!; public double Thick, A, Len; public bool Bright; }
    readonly List<GlowLayer> glowLayers = new(), cometLayers = new();
    GlowLayer hardLine = null!;
    double total, head;
    long lastFrameMs;
    Rectangle washRect = null!;
    double cr = 255, cg = 138, cb = 43, tr = 255, tg = 138, tb = 43;     // warna sekarang & target
    double level, runMax = 0.1;

    // tampilan
    Grid lyricView = null!, coverView = null!;
    StackPanel clockBox = null!;
    TextBlock timeText = null!, dateText = null!;
    TextBlock prev2 = null!, prev1 = null!, cur = null!, next1 = null!, next2 = null!;
    StackPanel prevStack = null!, nextStack = null!;
    readonly TranslateTransform ttPrev = new(), ttNext = new();
    readonly ScaleTransform curScale = new(1, 1), artScale = new(1, 1);
    readonly GradientStop progA = new(Colors.White, 0), progB = new(Colors.White, 0.0), progC = new(Color.FromArgb(0x66, 255, 255, 255), 0.04), progD = new(Color.FromArgb(0x66, 255, 255, 255), 1);
    Border bigArt = null!, smallArt = null!, bar = null!, fill = null!, playBtn = null!;
    TextBlock bigNote = null!, smallNote = null!, titleBig = null!, artistBig = null!, coverLine = null!, titleSmall = null!, artistSmall = null!;
    TextBlock playGlyph = null!, viewGlyph = null!, posText = null!, remText = null!;
    Grid track = null!;

    // keadaan
    MusicSnap snap = new();
    List<(double t, string l)>? lyr;
    string lyrState = "";
    ImageSource? curArt;
    long snapAt, lastPoll, lastMove;
    int lastIdx = -99, lyrVer, lastVer = -1, frame;
    bool lyricShown, forceCover, idle, closing, hasLyr;

    public MusicModeWindow(MainWindow owner)
    {
        main = owner;
        W = SystemParameters.PrimaryScreenWidth; H = SystemParameters.PrimaryScreenHeight;
        sc = Math.Clamp(H / 1080.0, 0.65, 1.5);

        Title = "NexoIsland - Mode Musik";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
        Background = Brushes.Transparent; Topmost = true; ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0; Top = 0; Width = W; Height = H;
        Opacity = 0;
        var p = Prefs.Current;

        var root = new Grid { Background = new SolidColorBrush(Color.FromArgb(0xF6, 4, 5, 9)) };
        Content = root;

        // ---- cahaya: wash di tengah, gradasi di empat tepi, dan bingkai neon tipis ----
        var wash = washBr = new RadialGradientBrush { Center = new Point(0.5, 0.46), GradientOrigin = new Point(0.5, 0.46), RadiusX = 0.72, RadiusY = 0.72 };
        wash.GradientStops.Add(washA); wash.GradientStops.Add(washB);
        washRect = new Rectangle { Fill = wash, IsHitTestVisible = false };
        root.Children.Add(washRect);

        BuildFrame(root);
        BuildBars(root);
        washRect.Visibility = p.MusicGlow ? Visibility.Visible : Visibility.Collapsed;
        foreach (var gl in glowLayers.Concat(cometLayers)) gl.P.Visibility = p.MusicGlow ? Visibility.Visible : Visibility.Collapsed;

        // ---- jam & tanggal ----
        timeText = new TextBlock { FontSize = 46 * sc, FontWeight = FontWeights.Light, Foreground = Brushes.White, Opacity = 0.92, HorizontalAlignment = HorizontalAlignment.Center, FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI") };
        dateText = new TextBlock { FontSize = 15 * sc, Foreground = Brushes.White, Opacity = 0.55, HorizontalAlignment = HorizontalAlignment.Center };
        clockBox = new StackPanel { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 82 * sc, 0, 0), IsHitTestVisible = false };
        clockBox.Children.Add(timeText); clockBox.Children.Add(dateText);
        clockBox.Visibility = p.MusicClock ? Visibility.Visible : Visibility.Collapsed;
        root.Children.Add(clockBox);

        BuildLyricView(root);
        BuildFxView(root);
        BuildCoverView(root);
        BuildBar(root);

        // tombol tutup & kredit
        var close = Btn("✕", 17 * sc, 46 * sc, () => CloseAnimated(), 0x18, "Segoe UI Symbol");
        close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, 34 * sc, 38 * sc, 0);
        close.ToolTip = "Tutup (Esc)";
        root.Children.Add(close);
        root.Children.Add(new TextBlock
        {
            Text = "NexoIsland  ·  class_programmer/farez", FontSize = 12 * sc, Foreground = Brushes.White, Opacity = 0.28,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(40 * sc, 0, 0, 34 * sc), IsHitTestVisible = false
        });

        BuildToast(root);

        Activated += (_, _) => Dispatcher.BeginInvoke(new Action(KeepOrder));
        PreviewKeyDown += OnKey;
        MouseMove += (_, _) => { lastMove = clk.ElapsedMilliseconds; if (idle) SetIdle(false); };
        Loaded += (_, _) =>
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(480)));
            Activate(); Focus();
            lastMove = clk.ElapsedMilliseconds;
            Poll(lastMove);
        };
        CompositionTarget.Rendering += OnFrame;
        Closed += (_, _) => CompositionTarget.Rendering -= OnFrame;
    }

    // ===================== Membangun tampilan =====================
    // ---------- bingkai cahaya: mengelilingi layar & MENGIKUTI BENTUK DYNAMIC ISLAND di tengah atas ----------
    static Point Bez(Point a, Point b, Point c, Point d, double t)
    {
        double u = 1 - t;
        return new Point(u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * c.X + t * t * t * d.X,
                         u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * c.Y + t * t * t * d.Y);
    }

    static void Arc(List<Point> pts, double cx, double cy, double r, double a0, double a1)
    {
        int n = Math.Max(4, (int)(Math.Abs(a1 - a0) / 5));
        for (int i = 0; i <= n; i++)
        {
            double a = (a0 + (a1 - a0) * i / n) * Math.PI / 180;
            pts.Add(new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
        }
    }

    // iw, ih = ukuran island SAAT INI (berubah saat dibuka / ditekan / HUD / notifikasi)
    List<Point> FramePoints(double iw, double ih)
    {
        const double m = 9, rc = 17, g = 8, e = 16;   // jarak bingkai dari tepi layar, radius sudut, celah ke island, lengkung atas island
        double cx = W / 2;
        double half = iw / 2, body = half - e, bot = ih, r = Math.Min(22, ih - e - 2), cr0 = r + g;
        double yy = Math.Min(26, bot - r);
        var pts = new List<Point> { new Point(m + rc, m), new Point(cx - half - g - 22, m) };

        for (int i = 1; i <= 18; i++)                // turun menyusuri sisi kiri island
            pts.Add(Bez(new Point(cx - half - g - 22, m), new Point(cx - half - g + 4, m), new Point(cx - body - g, 12), new Point(cx - body - g, yy), i / 18.0));
        Arc(pts, cx - body + r, bot - r, cr0, 180, 90);          // sudut kiri-bawah island
        Arc(pts, cx + body - r, bot - r, cr0, 90, 0);            // sudut kanan-bawah island
        for (int i = 1; i <= 18; i++)                // naik lagi di sisi kanan island
            pts.Add(Bez(new Point(cx + body + g, yy), new Point(cx + body + g, 12), new Point(cx + half + g - 4, m), new Point(cx + half + g + 22, m), i / 18.0));

        Arc(pts, W - m - rc, m + rc, rc, -90, 0);
        Arc(pts, W - m - rc, H - m - rc, rc, 0, 90);
        Arc(pts, m + rc, H - m - rc, rc, 90, 180);
        Arc(pts, m + rc, m + rc, rc, 180, 270);
        return pts;
    }

    StreamGeometry MakeFrameGeo(double iw, double ih)
    {
        var pts = FramePoints(iw, ih);
        total = 0;
        for (int i = 1; i < pts.Count; i++) total += (pts[i] - pts[i - 1]).Length;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], false, true);
            ctx.PolyLineTo(pts.Skip(1).ToList(), true, true);
        }
        geo.Freeze();
        return geo;
    }

    double frW = 190, frH = 48;

    // bingkai cahaya dibangun ulang tiap ukuran island berubah, jadi lekuknya ikut membesar / mengecil
    void SyncFrame()
    {
        var (iw, ih) = main.IslandSize();
        if (Math.Abs(iw - frW) < 0.4 && Math.Abs(ih - frH) < 0.4) return;
        frW = iw; frH = ih;
        var g = MakeFrameGeo(iw, ih);
        foreach (var gl in glowLayers) gl.P.Data = g;
        foreach (var gl in cometLayers)
        {
            gl.P.Data = g;
            gl.P.StrokeDashArray = new DoubleCollection { gl.Len / gl.Thick, Math.Max(0.01, (total / 2 - gl.Len) / gl.Thick) };
        }
    }

    // island & salju harus tetap di atas Mode Musik (Mode Musik bisa naik ke atas saat diklik / diaktifkan)
    IntPtr myHwnd;
    void KeepOrder()
    {
        if (closing) return;
        if (myHwnd == IntPtr.Zero) myHwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var mh = main.Hwnd;
        if (mh == IntPtr.Zero || myHwnd == IntPtr.Zero) return;
        var snow = App.Snow;
        bool islandOk = Native.IsAbove(mh, myHwnd);
        bool snowOk = snow == null || !snow.IsVisible || snow.Handle == IntPtr.Zero || Native.IsAbove(snow.Handle, myHwnd);
        if (islandOk && snowOk) return;
        snow?.Raise();
        main.RaiseSelf();
    }

    void BuildFrame(Grid root)
    {
        (frW, frH) = main.IslandSize();
        var geo = MakeFrameGeo(frW, frH);

        GlowLayer Add(List<GlowLayer> list, double thick, double a, double len, bool bright)
        {
            var br = new SolidColorBrush(Colors.White);
            var path = new WPath { Data = geo, Stroke = br, StrokeThickness = thick, StrokeLineJoin = PenLineJoin.Round, Opacity = a, IsHitTestVisible = false };
            if (len > 0)
            {
                path.StrokeDashCap = PenLineCap.Round;
                // dua komet berlawanan arah: pola berulang tiap setengah keliling
                path.StrokeDashArray = new DoubleCollection { len / thick, Math.Max(0.01, (total / 2 - len) / thick) };
            }
            var gl = new GlowLayer { P = path, Br = br, Thick = thick, A = a, Len = len, Bright = bright };
            list.Add(gl); root.Children.Add(path);
            return gl;
        }

        // cahaya lembut di sepanjang bingkai (mengikuti lekuk island)
        foreach (var (tk, al) in new[] { (70.0, 0.05), (48.0, 0.07), (30.0, 0.10), (17.0, 0.15), (8.0, 0.24) }) Add(glowLayers, tk * sc, al, 0, false);
        hardLine = Add(glowLayers, 2.5, 0.8, 0, true);

        // komet cahaya yang berputar mengelilingi layar & melewati island
        Add(cometLayers, 60 * sc, 0.10, 360 * sc, false);
        Add(cometLayers, 30 * sc, 0.20, 460 * sc, false);
        Add(cometLayers, 13 * sc, 0.45, 340 * sc, false);
        Add(cometLayers, 4, 0.22, 880 * sc, true);
        Add(cometLayers, 4, 0.50, 580 * sc, true);
        Add(cometLayers, 4, 1.00, 300 * sc, true);
    }

    TextBlock Txt(double size, double opacity, FontWeight weight) => new()
    {
        FontSize = size, Opacity = opacity, FontWeight = weight, Foreground = Brushes.White, TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = W * 0.78,
        FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"), Margin = new Thickness(0, 5 * sc, 0, 5 * sc), Visibility = Visibility.Collapsed
    };

    void BuildLyricView(Grid root)
    {
        lyricView = new Grid { Margin = new Thickness(W * 0.08, 140 * sc, W * 0.08, 200 * sc), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        lyricView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        lyricView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        lyricView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        prev2 = Txt(24 * sc, 0.16, FontWeights.SemiBold); prev1 = Txt(36 * sc, 0.34, FontWeights.SemiBold);
        next1 = Txt(36 * sc, 0.34, FontWeights.SemiBold); next2 = Txt(24 * sc, 0.16, FontWeights.SemiBold);
        cur = Txt(70 * sc, 1, FontWeights.Bold);
        cur.Margin = new Thickness(0, 14 * sc, 0, 14 * sc);
        cur.RenderTransformOrigin = new Point(0.5, 0.5); cur.RenderTransform = curScale;
        var prog = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        foreach (var g in new[] { progA, progB, progC, progD }) prog.GradientStops.Add(g);
        cur.Foreground = prog;
        cur.Effect = new DropShadowEffect { Color = Color.FromRgb(255, 138, 43), BlurRadius = 34, ShadowDepth = 0, Opacity = 0.45 };

        prevStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, ClipToBounds = true, RenderTransform = ttPrev };
        prevStack.Children.Add(prev2); prevStack.Children.Add(prev1);
        nextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Top, ClipToBounds = true, RenderTransform = ttNext };
        nextStack.Children.Add(next1); nextStack.Children.Add(next2);
        Grid.SetRow(prevStack, 0); Grid.SetRow(cur, 1); Grid.SetRow(nextStack, 2);
        lyricView.Children.Add(prevStack); lyricView.Children.Add(cur); lyricView.Children.Add(nextStack);
        root.Children.Add(lyricView);
    }

    void BuildCoverView(Grid root)
    {
        coverView = new Grid { Margin = new Thickness(0, 90 * sc, 0, 170 * sc), IsHitTestVisible = false };
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };

        double art = 340 * sc;
        var glowBr = new RadialGradientBrush();
        glowBr.GradientStops.Add(glowA); glowBr.GradientStops.Add(glowB);
        var artHolder = new Grid { Width = art, Height = art, HorizontalAlignment = HorizontalAlignment.Center };
        artHolder.Children.Add(new Ellipse { Width = art * 1.9, Height = art * 1.9, Fill = glowBr, IsHitTestVisible = false });

        bigArt = new Border
        {
            Width = art, Height = art, CornerRadius = new CornerRadius(26 * sc), Background = new SolidColorBrush(Color.FromRgb(0x20, 0x22, 0x2C)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 255, 255, 255)), BorderThickness = new Thickness(1),
            RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = artScale
        };
        bigNote = new TextBlock { Text = "♪", FontSize = art * 0.4, Foreground = Brushes.White, Opacity = 0.35, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        bigArt.Child = bigNote;
        artHolder.Children.Add(bigArt);
        stack.Children.Add(artHolder);

        titleBig = new TextBlock { Text = "Tidak ada musik yang diputar", FontSize = 38 * sc, FontWeight = FontWeights.Bold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = W * 0.7, Margin = new Thickness(0, 34 * sc, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI") };
        artistBig = new TextBlock { FontSize = 22 * sc, Foreground = Brushes.White, Opacity = 0.65, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = W * 0.7, Margin = new Thickness(0, 4 * sc, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        coverLine = new TextBlock { FontSize = 20 * sc, Foreground = Brushes.White, Opacity = 0.5, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = W * 0.6, Margin = new Thickness(0, 22 * sc, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(titleBig); stack.Children.Add(artistBig); stack.Children.Add(coverLine);
        coverView.Children.Add(stack);
        root.Children.Add(coverView);
    }

    void BuildBar(Grid root)
    {
        bar = new Border
        {
            Width = Math.Min(780 * sc, W * 0.8), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 46 * sc), Padding = new Thickness(20 * sc, 14 * sc, 20 * sc, 12 * sc), CornerRadius = new CornerRadius(30 * sc),
            Background = new SolidColorBrush(Color.FromArgb(0xB4, 14, 15, 22)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 255, 255, 255)), BorderThickness = new Thickness(1)
        };
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        smallArt = new Border { Width = 60 * sc, Height = 60 * sc, CornerRadius = new CornerRadius(12 * sc), Background = new SolidColorBrush(Color.FromRgb(0x20, 0x22, 0x2C)), Margin = new Thickness(0, 0, 16 * sc, 0) };
        smallNote = new TextBlock { Text = "♪", FontSize = 26 * sc, Foreground = Brushes.White, Opacity = 0.4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        smallArt.Child = smallNote;
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleSmall = new TextBlock { Text = "Tidak ada musik yang diputar", FontSize = 17 * sc, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis };
        artistSmall = new TextBlock { FontSize = 13 * sc, Foreground = Brushes.White, Opacity = 0.6, TextTrimming = TextTrimming.CharacterEllipsis };
        info.Children.Add(titleSmall); info.Children.Add(artistSmall);

        var ctl = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12 * sc, 0, 0, 0) };
        viewGlyph = null!;
        var glowBtn = Btn("✨", 17 * sc, 42 * sc, CycleGlow, 0x00, "Segoe UI Emoji");
        glowBtn.ToolTip = "Ganti mode cahaya (G)";
        var styleBtn = Btn("Aa", 15 * sc, 42 * sc, CycleLyric, 0x00, "Segoe UI");
        styleBtn.ToolTip = "Ganti mode lirik (L)";
        ctl.Children.Add(glowBtn);
        ctl.Children.Add(styleBtn);
        var viewBtn = Btn("🖼", 17 * sc, 42 * sc, ToggleView, 0x00, "Segoe UI Emoji");
        viewBtn.ToolTip = "Ganti tampilan: lirik / cover (C)";
        viewGlyph = (TextBlock)viewBtn.Child;
        ctl.Children.Add(viewBtn);
        ctl.Children.Add(Btn("⏮", 19 * sc, 44 * sc, () => main.MusicCtl(-1), 0x00, "Segoe UI Symbol"));
        playBtn = Btn("▶", 21 * sc, 54 * sc, () => main.MusicCtl(0), 0xFF, "Segoe UI Symbol");
        playGlyph = (TextBlock)playBtn.Child; playGlyph.Foreground = Brushes.Black;
        playBtn.Margin = new Thickness(6 * sc, 0, 6 * sc, 0);
        ctl.Children.Add(playBtn);
        ctl.Children.Add(Btn("⏭", 19 * sc, 44 * sc, () => main.MusicCtl(1), 0x00, "Segoe UI Symbol"));

        Grid.SetColumn(smallArt, 0); Grid.SetColumn(info, 1); Grid.SetColumn(ctl, 2);
        top.Children.Add(smallArt); top.Children.Add(info); top.Children.Add(ctl);

        var prow = new Grid { Margin = new Thickness(0, 12 * sc, 0, 0) };
        prow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        prow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        prow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        posText = new TextBlock { Text = "00:00", FontSize = 12 * sc, Foreground = Brushes.White, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        remText = new TextBlock { FontSize = 12 * sc, Foreground = Brushes.White, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        track = new Grid { Height = 20 * sc, Background = Brushes.Transparent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
        track.Children.Add(new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromArgb(0x33, 255, 255, 255)), VerticalAlignment = VerticalAlignment.Center });
        fill = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Width = 0 };
        track.Children.Add(fill);
        track.MouseLeftButtonDown += (s, e) =>
        {
            if (snap.Dur <= 0) return;
            double f = Math.Clamp(e.GetPosition(track).X / Math.Max(1, track.ActualWidth), 0, 1);
            snap.Pos = f * snap.Dur; snapAt = clk.ElapsedMilliseconds;
            main.MusicSeek(f * snap.Dur);
            e.Handled = true;
        };
        Grid.SetColumn(posText, 0); Grid.SetColumn(track, 1); Grid.SetColumn(remText, 2);
        prow.Children.Add(posText); prow.Children.Add(track); prow.Children.Add(remText);

        Grid.SetRow(top, 0); Grid.SetRow(prow, 1);
        g.Children.Add(top); g.Children.Add(prow);
        bar.Child = g;
        root.Children.Add(bar);
    }

    // tombol bulat sederhana (tanpa template). baseA = transparansi latar (0xFF = putih penuh)
    static Border Btn(string glyph, double size, double box, Action click, byte baseA, string font)
    {
        var tb = new TextBlock
        {
            Text = glyph, FontSize = size, Foreground = Brushes.White, FontFamily = new FontFamily(font),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        var bg = new SolidColorBrush(Color.FromArgb(baseA, 255, 255, 255));
        var b = new Border { Width = box, Height = box, CornerRadius = new CornerRadius(box / 2), Background = bg, Cursor = Cursors.Hand, Child = tb };
        byte hov = baseA == 0xFF ? (byte)0xDD : (byte)0x34;
        b.MouseEnter += (_, _) => bg.Color = Color.FromArgb(hov, 255, 255, 255);
        b.MouseLeave += (_, _) => bg.Color = Color.FromArgb(baseA, 255, 255, 255);
        b.MouseLeftButtonUp += (_, e) => { click(); e.Handled = true; };
        return b;
    }

    // ===================== Kontrol =====================
    void OnKey(object s, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: CloseAnimated(); break;
            case Key.Space: main.MusicCtl(0); break;
            case Key.Left: main.MusicCtl(-1); break;
            case Key.Right: main.MusicCtl(1); break;
            case Key.C: ToggleView(); break;
            case Key.G: CycleGlow(); break;
            case Key.L: CycleLyric(); break;
            default: return;
        }
        e.Handled = true;
    }

    void ToggleView()
    {
        if (!hasLyr) return;                  // tanpa lirik hanya ada tampilan cover
        forceCover = !forceCover;
        ApplyView();
    }

    public void CloseAnimated()
    {
        if (closing) return;
        closing = true;
        var a = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(260));
        a.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, a);
    }

    void SetIdle(bool on)
    {
        idle = on;
        Cursor = on ? Cursors.None : Cursors.Arrow;
        bar.BeginAnimation(OpacityProperty, new DoubleAnimation(on ? 0.12 : 1, TimeSpan.FromMilliseconds(on ? 700 : 200)));
    }

    // ===================== Data musik =====================
    static Color Vivid(Color c)
    {
        double r = c.R, g = c.G, b = c.B, m = Math.Max(r, Math.Max(g, b));
        if (m < 1) return Color.FromRgb(0xFF, 0x8A, 0x2B);
        double f = m < 170 ? Math.Min(5, 190 / m) : 1;     // cover gelap dicerahkan supaya cahaya tetap terlihat
        return Color.FromRgb((byte)Math.Min(255, r * f), (byte)Math.Min(255, g * f), (byte)Math.Min(255, b * f));
    }

    void SetArt(ImageSource? a)
    {
        curArt = a;
        Brush Mk() => a != null ? new ImageBrush(a) { Stretch = Stretch.UniformToFill } : new SolidColorBrush(Color.FromRgb(0x20, 0x22, 0x2C));
        bigArt.Background = Mk(); smallArt.Background = Mk();
        bigNote.Visibility = smallNote.Visibility = a == null ? Visibility.Visible : Visibility.Collapsed;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        artScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(600)) { EasingFunction = ease });
        artScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(600)) { EasingFunction = ease });
    }

    void Poll(long now)
    {
        MusicSnap s;
        try { s = main.GetMusicSnap(); } catch { return; }
        snap = s; snapAt = now;

        bool none = string.IsNullOrEmpty(s.Title) || s.Title == "Tidak ada musik";
        string head = none ? "Tidak ada musik yang diputar" : s.Title;
        if (titleBig.Text != head) { titleBig.Text = head; titleSmall.Text = head; }
        if (artistBig.Text != s.Artist) { artistBig.Text = s.Artist; artistSmall.Text = s.Artist; }
        if (!ReferenceEquals(s.Art, curArt)) SetArt(s.Art);

        var c = Vivid(s.HasArt ? s.Color : Prefs.Current.C1);
        tr = c.R; tg = c.G; tb = c.B;
        playGlyph.Text = s.Playing ? "⏸" : "▶";

        if (!ReferenceEquals(s.Lyrics, lyr)) { lyr = s.Lyrics; lyrVer++; }
        if (s.LyricState != lyrState) { lyrState = s.LyricState; lyrVer++; }
        bool has = lyr != null && lyr.Count > 0;
        if (has != hasLyr) { hasLyr = has; ApplyView(); }
    }

    void UpdateLines(int idx)
    {
        string L(int i) => lyr != null && i >= 0 && i < lyr.Count ? lyr[i].l : "";
        void Set(TextBlock t, string s) { t.Text = s; t.Visibility = s.Length == 0 ? Visibility.Collapsed : Visibility.Visible; }

        string now = idx >= 0 ? L(idx) : "";
        if (now.Length == 0) now = "♪";
        Set(prev2, L(idx - 2)); Set(prev1, L(idx - 1));
        Set(next1, L(idx + 1)); Set(next2, L(idx + 2));
        cur.Text = now; cur.Visibility = Visibility.Visible;
        double k = now.Length > 70 ? 0.62 : now.Length > 40 ? 0.8 : 1;       // baris panjang diperkecil
        cur.FontSize = 70 * sc * k;

        // geser naik + baris aktif muncul membesar
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        double d = 62 * sc;
        ttPrev.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(d, 0, TimeSpan.FromMilliseconds(560)) { EasingFunction = ease });
        ttNext.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(d, 0, TimeSpan.FromMilliseconds(560)) { EasingFunction = ease });
        cur.BeginAnimation(OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(420)));
        curScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(520)) { EasingFunction = ease });
        curScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(520)) { EasingFunction = ease });
    }

    static string Fmt(double s) => TimeSpan.FromSeconds(Math.Max(0, s)).ToString(@"mm\:ss");

    // ===================== Per frame =====================
    static byte A(double v) => (byte)Math.Clamp(v, 0, 255);

    void OnFrame(object? sender, EventArgs e)
    {
        long now = clk.ElapsedMilliseconds;
        frame++;
        if (now - lastPoll >= 120) { lastPoll = now; Poll(now); }
        if (frame % 15 == 0) KeepOrder();
        if (!idle && now - lastMove > 4500) SetIdle(true);

        double pos = snap.Pos + (snap.Playing ? (now - snapAt) / 1000.0 : 0);
        if (snap.Dur > 0) pos = Math.Min(pos, snap.Dur);

        // ---- lirik ----
        int idx = -1;
        if (lyr != null)
            for (int i = 0; i < lyr.Count; i++) { if (lyr[i].t <= pos + 0.25) idx = i; else break; }
        if (idx != lastIdx || lyrVer != lastVer)
        {
            lastIdx = idx; lastVer = lyrVer;
            if (lyricShown) { if (LM == "classic") UpdateLines(idx); else FxLine(idx); }
            string line = idx >= 0 && lyr != null ? lyr[idx].l : "";
            coverLine.Text = hasLyr ? line : lyrState;
        }
        if (lyricShown)
        {
            double p = LineProgress(idx, pos);
            if (LM == "classic") { if (frame % 3 == 0) { progB.Offset = p; progC.Offset = Math.Min(1, p + 0.04); } }
            else FxFrame(p, now);
        }

        // ---- cahaya ----
        cr += (tr - cr) * 0.05; cg += (tg - cg) * 0.05; cb += (tb - cb) * 0.05;
        if (frame % 2 == 0)
        {
            var (ok, pk) = Native.Peak();
            double target;
            if (!snap.Playing) target = 0;
            else if (ok)
            {
                runMax = Math.Max(pk, runMax * 0.9992);                       // normalisasi: lagu pelan / volume kecil tetap terlihat berdenyut
                target = Math.Min(1, pk / Math.Max(0.12, runMax));
            }
            else target = 0.5 + 0.5 * Math.Sin(now / 650.0);                  // meter tidak tersedia: denyut lembut
            level += (target - level) * (target > level ? 0.55 : 0.10);
        }
        double inten = snap.Playing ? 0.52 + 0.48 * level : 0.3;
        byte R = A(cr), G = A(cg), B = A(cb);
        SyncFrame();
        UpdateGlow(now, inten, Color.FromRgb(R, G, B));
        glowA.Color = Color.FromArgb(A(70 + 70 * inten), R, G, B); glowB.Color = Color.FromArgb(0, R, G, B);
        lastFrameMs = now;
        if (frame % 6 == 0 && cur.Effect is DropShadowEffect ds) ds.Color = Color.FromRgb(R, G, B);

        // ---- bar pemutar & jam ----
        if (frame % 2 == 0)
        {
            fill.Width = snap.Dur > 0 ? track.ActualWidth * Math.Clamp(pos / snap.Dur, 0, 1) : 0;
            posText.Text = Fmt(pos);
            remText.Text = snap.Dur > 0 ? "-" + Fmt(snap.Dur - pos) : "";
        }
        if (frame % 30 == 1)
        {
            var pp = Prefs.Current;
            if (LM != shownLm) ApplyView();                       // mode lirik diganti dari Pengaturan
            clockBox.Visibility = pp.MusicClock ? Visibility.Visible : Visibility.Collapsed;
            var n = DateTime.Now;
            timeText.Text = n.ToString("HH:mm");
            dateText.Text = n.ToString("dddd, d MMMM", CultureInfo.GetCultureInfo("id-ID"));
        }
    }
}
