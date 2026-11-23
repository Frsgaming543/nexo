using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
namespace NexoIsland;

// MODE MUSIK - bagian 2: banyak MODE CAHAYA (tepi layar) dan MODE LIRIK.
//   G = ganti mode cahaya | L = ganti mode lirik (juga lewat tombol ✨ / Aa di panel pemutar & Pengaturan > MODE MUSIK)
//   Pengembang: class_programmer/farez
public partial class MusicModeWindow
{
    public static readonly (string key, string name)[] GlowModes =
    {
        ("comet", "Komet"), ("pulse", "Denyut"), ("breathe", "Napas"), ("aurora", "Aurora"),
        ("rainbow", "Pelangi"), ("neon", "Neon"), ("ambient", "Ambient"), ("spectrum", "Spektrum")
    };

    public static readonly (string key, string name)[] LyricModes =
    {
        ("classic", "Klasik"), ("code", "Kode"), ("constellation", "Konstelasi"), ("karaoke", "Per kata"),
        ("typewriter", "Mesin ketik"), ("neon", "Neon"), ("wave", "Gelombang"), ("scroll", "Gulir"), ("cover", "Cover + teks")
    };

    // ---------- bahan umum ----------
    static readonly FontFamily Ui = new("Segoe UI Variable Display, Segoe UI");
    static readonly FontFamily Mono = new("Cascadia Code, Cascadia Mono, Consolas");

    static SolidColorBrush Fb(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    static readonly Brush[] CodePal = { Fb("#FF6AA2"), Fb("#F4F1EA"), Fb("#FFC857"), Fb("#FF9F43"), Fb("#5CE1E6"), Fb("#9BE564") };
    static readonly Brush CodeNum = Fb("#5B6272"), CodePunc = Fb("#B8BCC8"), CodeLast = Fb("#FFE066");

    Color Accent => Color.FromRgb(A(cr), A(cg), A(cb));
    Color AccentHot => Theme.Mix(Accent, Colors.White, 0.55);

    static (double h, double s, double v) ToHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), d = mx - mn, h = 0;
        if (d > 1e-9)
        {
            if (mx == r) h = 60 * (((g - b) / d) % 6);
            else if (mx == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
        }
        if (h < 0) h += 360;
        return (h, mx <= 0 ? 0 : d / mx, mx);
    }

    static Color FromHsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360; s = Math.Clamp(s, 0, 1); v = Math.Clamp(v, 0, 1);
        double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c, r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    static void Vis(UIElement e, bool on)
    {
        var v = on ? Visibility.Visible : Visibility.Collapsed;
        if (e.Visibility != v) e.Visibility = v;
    }

    static double Flicker(double t)
    {
        double f = Math.Sin(t * 53) * Math.Sin(t * 17.3 + 1);
        if (f > 0.93) return 0.45;
        return Math.Sin(t * 9.1) > 0.985 ? 0.7 : 1;
    }

    // ===================== Toast & ganti mode =====================
    Border toast = null!;
    TextBlock toastText = null!;

    void BuildToast(Grid root)
    {
        toastText = new TextBlock { FontSize = 15 * sc, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold };
        toast = new Border
        {
            Child = toastText, Padding = new Thickness(16 * sc, 9 * sc, 16 * sc, 9 * sc), CornerRadius = new CornerRadius(20 * sc),
            Background = new SolidColorBrush(Color.FromArgb(0xC8, 14, 15, 22)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 255, 255, 255)), BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(40 * sc, 34 * sc, 0, 0), IsHitTestVisible = false, Opacity = 0
        };
        root.Children.Add(toast);
    }

    void Toast(string s)
    {
        toastText.Text = s;
        toast.BeginAnimation(OpacityProperty, null);
        toast.Opacity = 1;
        toast.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(700)) { BeginTime = TimeSpan.FromMilliseconds(1600) });
    }

    void CycleGlow()
    {
        int i = Array.FindIndex(GlowModes, m => m.key == Prefs.Current.GlowMode);
        i = (i + 1) % GlowModes.Length;
        Prefs.Current.GlowMode = GlowModes[i].key;
        Prefs.Current.MusicGlow = true;
        Prefs.Apply();
        Toast($"✨  Cahaya: {GlowModes[i].name}   ({i + 1}/{GlowModes.Length})   ·   G = ganti");
    }

    void CycleLyric()
    {
        int i = Array.FindIndex(LyricModes, m => m.key == LM);
        i = (i + 1) % LyricModes.Length;
        Prefs.Current.LyricMode = LyricModes[i].key;
        Prefs.Apply();
        if (hasLyr && forceCover) { forceCover = false; }
        ApplyView();
        Toast($"Aa  Lirik: {LyricModes[i].name}   ({i + 1}/{LyricModes.Length})   ·   L = ganti");
    }

    // ===================== MODE CAHAYA =====================
    readonly Rectangle[] bars = new Rectangle[56], halos = new Rectangle[56];
    readonly double[] barVal = new double[56];
    readonly SolidColorBrush barBr = new(Colors.White);
    Canvas barCanvas = null!;
    RadialGradientBrush washBr = null!;
    bool ambientSet;

    void BuildBars(Grid root)
    {
        barCanvas = new Canvas { Width = W, Height = H, IsHitTestVisible = false, Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        int n = bars.Length;
        double step = (W - 80 * sc) / n, bw = step * 0.6;
        for (int i = 0; i < n; i++)
        {
            double x = 40 * sc + i * step + (step - bw) / 2;
            var hl = new Rectangle { Width = bw * 2.4, Height = 8, RadiusX = bw * 1.2, RadiusY = bw * 1.2, Fill = barBr, Opacity = 0.18 };
            var r = new Rectangle { Width = bw, Height = 6, RadiusX = bw / 2, RadiusY = bw / 2, Fill = barBr, Opacity = 0.92 };
            Canvas.SetLeft(hl, x - bw * 0.7); Canvas.SetTop(hl, H - 8);
            Canvas.SetLeft(r, x); Canvas.SetTop(r, H - 6);
            halos[i] = hl; bars[i] = r;
            barCanvas.Children.Add(hl); barCanvas.Children.Add(r);
        }
        root.Children.Add(barCanvas);
    }

    // dipanggil tiap frame
    void UpdateGlow(long now, double inten, Color tint)
    {
        var pp = Prefs.Current;
        bool on = pp.MusicGlow;
        string mode = Array.Exists(GlowModes, m => m.key == pp.GlowMode) ? pp.GlowMode : "comet";
        Vis(washRect, on);
        if (!on)
        {
            foreach (var gl in glowLayers) Vis(gl.P, false);
            foreach (var gl in cometLayers) Vis(gl.P, false);
            Vis(barCanvas, false);
            return;
        }

        double t = now / 1000.0;
        double dt = Math.Min(0.1, (now - lastFrameMs) / 1000.0);
        var hsv = ToHsv(tint);
        double lv = snap.Playing ? level : 0;
        double br = 0.5 + 0.5 * Math.Sin(t * 1.15);
        bool comets = mode is "comet" or "aurora" or "rainbow";
        bool barsOn = mode == "spectrum";
        double op, hardOp, wAlpha, k = 1;

        switch (mode)
        {
            case "pulse": op = 0.3 + 1.5 * lv; hardOp = 0.45 + 0.8 * lv; k = 0.75 + 0.9 * lv; wAlpha = 30 + 110 * lv; break;
            case "breathe": op = 0.4 + 1.0 * br; hardOp = 0.55 + 0.6 * br; wAlpha = 35 + 70 * br; break;
            case "aurora": op = 0.7 + 0.9 * inten; hardOp = 1.0; wAlpha = 50 + 60 * inten; break;
            case "rainbow": op = 0.9 + 0.7 * inten; hardOp = 1.1; wAlpha = 45 + 55 * inten; break;
            case "neon":
            {
                double fl = Flicker(t);
                op = (0.55 + 0.7 * inten) * fl; hardOp = 1.25 * fl; wAlpha = (20 + 30 * inten) * fl; break;
            }
            case "ambient": op = 0.3; hardOp = 0.4; wAlpha = 70 + 110 * inten; break;
            case "spectrum": op = 0.45; hardOp = 0.6; wAlpha = 35 + 50 * inten; break;
            default: op = Math.Clamp(0.55 + 1.1 * inten, 0, 1.6); hardOp = 0.55 + 0.45 * inten; wAlpha = 52 + 60 * inten; break;
        }

        // cahaya lembut di sepanjang bingkai
        int gi = 0;
        foreach (var gl in glowLayers)
        {
            bool hard = gl == hardLine;
            Vis(gl.P, mode != "neon" || hard || gi >= 3);
            Color c = mode switch
            {
                "aurora" => FromHsv(hsv.h + 38 * Math.Sin(t * 0.6 + gi * 0.8), hsv.s, hsv.v),
                "rainbow" => FromHsv(t * 50 + gi * 26, 0.9, 1),
                _ => tint
            };
            if (gl.Bright) c = Theme.Mix(c, Colors.White, mode == "neon" ? 0.55 : 0.25);
            gl.Br.Color = c;
            gl.P.Opacity = Math.Min(1, gl.A * (hard ? hardOp : op));
            double th = gl.Thick * (hard ? 1 : k);
            if (Math.Abs(gl.P.StrokeThickness - th) > 0.05) gl.P.StrokeThickness = th;
            gi++;
        }

        // komet berputar (Komet, Aurora, Pelangi)
        if (comets) head = (head + dt * (snap.Playing ? 300 + 760 * level : 120)) % (total / 2);
        int ci = 0;
        foreach (var gl in cometLayers)
        {
            Vis(gl.P, comets);
            if (comets)
            {
                Color c = mode switch
                {
                    "aurora" => FromHsv(hsv.h + (ci - 2.5) * 32, hsv.s, hsv.v),
                    "rainbow" => FromHsv(t * 60 + ci * 55, 0.85, 1),
                    _ => tint
                };
                if (gl.Bright) c = Theme.Mix(c, Colors.White, 0.55);
                gl.Br.Color = c;
                gl.P.Opacity = Math.Min(1, gl.A * (0.6 + 0.4 * inten));
                gl.P.StrokeDashOffset = (gl.Len - head) / gl.Thick;
            }
            ci++;
        }

        // kabut warna di tengah layar
        Color wc = mode == "rainbow" ? FromHsv(t * 50, 0.8, 1) : tint;
        if (mode == "ambient")
        {
            var cp = new Point(0.5 + 0.3 * Math.Cos(t * 0.35), 0.46 + 0.22 * Math.Sin(t * 0.27));
            washBr.Center = cp; washBr.GradientOrigin = cp; washBr.RadiusX = washBr.RadiusY = 0.95;
            ambientSet = true;
        }
        else if (ambientSet)
        {
            var c0 = new Point(0.5, 0.46);
            washBr.Center = c0; washBr.GradientOrigin = c0; washBr.RadiusX = washBr.RadiusY = 0.72;
            ambientSet = false;
        }
        washA.Color = Color.FromArgb(A(wAlpha), wc.R, wc.G, wc.B);
        washB.Color = Color.FromArgb(0, wc.R, wc.G, wc.B);

        // spektrum: batang naik dari bawah layar (disintesis dari level suara)
        Vis(barCanvas, barsOn);
        if (barsOn)
        {
            barBr.Color = Theme.Mix(tint, Colors.White, 0.2);
            double maxH = H * 0.24;
            int n = bars.Length;
            for (int i = 0; i < n; i++)
            {
                double d = Math.Abs(i - (n - 1) / 2.0) / ((n - 1) / 2.0);
                double wob = Math.Abs(Math.Sin(t * (1.9 + 0.11 * i) + i * 0.9) * Math.Sin(t * 0.8 + i * 0.37));
                double target = snap.Playing ? level * (0.2 + 0.8 * wob) * (1 - 0.55 * d) : 0.02;
                barVal[i] += (target - barVal[i]) * (target > barVal[i] ? 0.55 : 0.12);
                double h = 6 + barVal[i] * maxH;
                bars[i].Height = h; Canvas.SetTop(bars[i], H - h);
                halos[i].Height = h + 6; Canvas.SetTop(halos[i], H - h - 6);
            }
        }
    }

    // ===================== MODE LIRIK =====================
    Grid fxView = null!;
    string shownLm = "";
    bool fxReady;
    int fxIdx = -1;
    long fxStart;
    readonly SolidColorBrush caretBr = new(Colors.White);

    string LM
    {
        get
        {
            var m = Prefs.Current.LyricMode;
            return Array.Exists(LyricModes, x => x.key == m) ? m : "classic";
        }
    }

    void BuildFxView(Grid root)
    {
        fxView = new Grid { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        root.Children.Add(fxView);
    }

    string LineAt(int i) => lyr != null && i >= 0 && i < lyr.Count ? lyr[i].l : "";

    double LineProgress(int idx, double pos)
    {
        if (idx < 0 || lyr == null) return 0;
        double t0 = lyr[idx].t, t1 = idx + 1 < lyr.Count ? lyr[idx + 1].t : t0 + 5;
        return Math.Clamp((pos - t0) / Math.Max(0.6, t1 - t0 - 0.35), 0, 1);
    }

    TextBlock Tx(string text, double size, FontWeight w, Brush fg, double op = 1, FontFamily? ff = null) => new()
    {
        Text = text, FontSize = size, FontWeight = w, Foreground = fg, Opacity = op, FontFamily = ff ?? Ui, TextWrapping = TextWrapping.Wrap
    };

    TextBlock DimLine(string text, double size, double op)
    {
        var tb = Tx(text, size, FontWeights.SemiBold, Brushes.White, op);
        tb.TextAlignment = TextAlignment.Center; tb.HorizontalAlignment = HorizontalAlignment.Center; tb.MaxWidth = W * 0.7;
        tb.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        return tb;
    }

    static List<string> Words(string line)
    {
        var w = Regex.Split(line.Trim(), @"\s+").Where(x => x.Length > 0).ToList();
        if (w.Count == 0) w.Add("♪");
        return w;
    }

    // lirik ada -> tampilan sesuai mode; tanpa lirik / mode "cover" -> cover
    void ApplyView()
    {
        bool lyric = hasLyr && !forceCover;
        viewGlyph.Text = lyric ? "🖼" : "💬";
        string lm = LM;
        FrameworkElement want = !lyric || lm == "cover" ? coverView : lm == "classic" ? lyricView : fxView;
        bool same = lyric == lyricShown && lm == shownLm && want.Visibility == Visibility.Visible
                    && (want == coverView || coverView.Visibility != Visibility.Visible)
                    && (want == lyricView || lyricView.Visibility != Visibility.Visible)
                    && (want == fxView || fxView.Visibility != Visibility.Visible);
        if (same) return;
        lyricShown = lyric; shownLm = lm;
        foreach (var v in new FrameworkElement[] { lyricView, fxView, coverView })
            if (v != want) { v.BeginAnimation(OpacityProperty, null); v.Visibility = Visibility.Collapsed; }
        want.Visibility = Visibility.Visible;
        want.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(380)));
        lastIdx = -99;
        fxReady = false;
    }

    // baris lirik berganti -> bangun adegan baru
    void FxLine(int idx)
    {
        string mode = LM;
        if (mode == "classic" || mode == "cover") return;
        fxView.Children.Clear();
        fxIdx = idx; fxStart = clk.ElapsedMilliseconds;
        if (mode == "scroll") fxView.BeginAnimation(OpacityProperty, null);
        else fxView.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
        string line = LineAt(idx);
        if (line.Length == 0) line = "♪";
        switch (mode)
        {
            case "code": BuildCode(idx); break;
            case "constellation": BuildConstellation(idx, line); break;
            case "karaoke": BuildKaraoke(idx, line); break;
            case "typewriter": BuildTypewriter(idx, line); break;
            case "neon": BuildNeon(idx, line); break;
            case "wave": BuildWave(idx, line); break;
            case "scroll": BuildScroll(idx); break;
        }
        fxReady = true;
    }

    // dipanggil tiap frame selama mode lirik non-klasik
    void FxFrame(double p, long now)
    {
        if (!fxReady) return;
        caretBr.Color = AccentHot;
        switch (LM)
        {
            case "code": FrameCode(p, now); break;
            case "constellation": FrameConstellation(p, now); break;
            case "karaoke": FrameKaraoke(p); break;
            case "typewriter": FrameTypewriter(p, now); break;
            case "neon": FrameNeon(now); break;
            case "wave": FrameWave(p, now); break;
            case "scroll": if (frame % 3 == 0) { sgB.Offset = p; sgC.Offset = Math.Min(1, p + 0.04); } break;
        }
    }

    // ---------- 1. KODE: lirik diketik seperti di editor kode ----------
    TextBlock? codeCur;
    Run codeCaret = new("▌");
    string codeText = "";
    int codeN = -1;

    void BuildCode(int idx)
    {
        var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Width = Math.Min(W * 0.76, 1500 * sc) };
        double fs = 40 * sc;
        int cur = Math.Max(0, idx);
        codeCaret = new Run("▌") { Foreground = caretBr };      // Run baru per adegan (satu Run tidak boleh dipakai di dua TextBlock)
        codeCur = null;
        for (int i = Math.Max(0, cur - 5); i <= cur; i++)
        {
            bool isCur = i == cur;
            string text = idx < 0 && isCur ? "// ♪" : LineAt(i);
            if (text.Length == 0) text = "// ♪";
            int age = cur - i;
            var row = new Grid { Margin = new Thickness(0, 4 * sc, 0, 4 * sc), Opacity = age switch { 0 => 1.0, 1 => 0.72, 2 => 0.52, 3 => 0.4, _ => 0.3 } };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78 * sc) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var num = Tx((i + 1).ToString(), fs * 0.62, FontWeights.Normal, CodeNum, 1, Mono);
            num.TextAlignment = TextAlignment.Right; num.VerticalAlignment = VerticalAlignment.Top; num.Margin = new Thickness(0, fs * 0.28, 0, 0);
            var tb = Tx("", fs, FontWeights.Medium, Brushes.White, 1, Mono);
            tb.LineHeight = fs * 1.42; tb.Margin = new Thickness(26 * sc, 0, 0, 0);
            Grid.SetColumn(tb, 1);
            row.Children.Add(num); row.Children.Add(tb);
            st.Children.Add(row);
            if (isCur) { codeCur = tb; codeText = text; codeN = -1; }
            else Colorize(tb, text, text.Length, true, false);
        }
        fxView.Children.Add(st);
    }

    void Colorize(TextBlock tb, string text, int n, bool complete, bool caret)
    {
        tb.Inlines.Clear();
        string s = text.Substring(0, Math.Clamp(n, 0, text.Length));
        int total = Regex.Matches(text, @"[\p{L}\p{N}']+").Count, wi = 0;
        foreach (Match m in Regex.Matches(s, @"[\p{L}\p{N}']+|\s+|[^\p{L}\p{N}'\s]+"))
        {
            Brush b; FontWeight w = FontWeights.Medium;
            char c0 = m.Value[0];
            if (char.IsLetterOrDigit(c0) || c0 == '\'')
            {
                bool last = complete && wi == total - 1;
                b = last ? CodeLast : CodePal[(wi + Math.Max(0, fxIdx) * 2) % CodePal.Length];
                if (last) w = FontWeights.Bold;
                wi++;
            }
            else b = char.IsWhiteSpace(c0) ? Brushes.White : CodePunc;
            tb.Inlines.Add(new Run(m.Value) { Foreground = b, FontWeight = w });
        }
        if (caret) tb.Inlines.Add(codeCaret);
    }

    void FrameCode(double p, long now)
    {
        if (codeCur == null) return;
        int len = codeText.Length;
        int n = (int)Math.Ceiling(Math.Clamp(p / 0.72, 0, 1) * len);
        if (n != codeN) { codeN = n; Colorize(codeCur, codeText, n, n >= len, true); }
        codeCaret.Foreground = n < len || ((now / 450) & 1) == 0 ? caretBr : Brushes.Transparent;
    }

    // ---------- 2. KONSTELASI: kata = bintang yang saling terhubung ----------
    readonly List<Point> cPts = new();
    readonly List<(Line ln, int a, int b)> cEdges = new();
    readonly List<Ellipse> cDots = new();
    readonly List<TextBlock> cLbl = new();
    List<string> cWords = new();
    Canvas? cRing;
    TextBlock? cBig;
    readonly TranslateTransform cRingT = new(), cDrift = new();
    Point cPos;
    double cBigDx, cBigDy;
    int cShown = -1;

    void BuildConstellation(int idx, string line)
    {
        cPts.Clear(); cEdges.Clear(); cDots.Clear(); cLbl.Clear();
        var words = Words(line);
        while (words.Count > 12)
        {
            var mg = new List<string>();
            for (int i = 0; i < words.Count; i += 2) mg.Add(i + 1 < words.Count ? words[i] + " " + words[i + 1] : words[i]);
            words = mg;
        }
        cWords = words;
        int n = words.Count;
        var rnd = new Random(Math.Max(0, idx) * 7919 + 13);
        double top = 150 * sc, bot = H - 230 * sc, midY = (top + bot) / 2, ah = bot - top;
        double x0 = W * 0.16, xs = W * 0.68;
        for (int i = 0; i < n; i++)
        {
            double fx = n == 1 ? 0.5 : (double)i / (n - 1);
            double jx = (rnd.NextDouble() - 0.5) * (xs / n) * 0.45;
            double y = midY + Math.Sin(i * 1.45 + rnd.NextDouble()) * ah * 0.27 + (rnd.NextDouble() - 0.5) * ah * 0.16;
            cPts.Add(new Point(x0 + xs * fx + jx, y));
        }

        var edges = new List<(int a, int b)>();
        for (int i = 1; i < n; i++)
        {
            edges.Add((i - 1, i));
            if (i >= 2 && rnd.NextDouble() < 0.55) edges.Add((i - 2, i));
            if (i >= 3 && rnd.NextDouble() < 0.22) edges.Add((i - 3, i));
        }

        var rootC = new Canvas { Width = W, Height = H, RenderTransform = cDrift, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var lineC = new Canvas { Width = W, Height = H, Effect = new DropShadowEffect { Color = Accent, BlurRadius = 9, ShadowDepth = 0, Opacity = 0.9 } };
        var nodeC = new Canvas { Width = W, Height = H };
        foreach (var (a, b) in edges)
        {
            var ln = new Line
            {
                X1 = cPts[a].X, Y1 = cPts[a].Y, X2 = cPts[b].X, Y2 = cPts[b].Y,
                Stroke = Brushes.White, StrokeThickness = 1.3, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Opacity = 0
            };
            lineC.Children.Add(ln); cEdges.Add((ln, a, b));
        }
        for (int i = 0; i < n; i++)
        {
            double x = cPts[i].X, y = cPts[i].Y, r = 3.2 * sc;
            var dot = new Ellipse { Width = r * 2, Height = r * 2, Fill = Brushes.White, Opacity = 0 };
            Canvas.SetLeft(dot, x - r); Canvas.SetTop(dot, y - r);
            var lb = Tx(words[i], 13 * sc, FontWeights.Medium, Brushes.White, 0);
            lb.TextWrapping = TextWrapping.NoWrap;
            Canvas.SetLeft(lb, x + 7 * sc); Canvas.SetTop(lb, y + (i % 2 == 0 ? -22 * sc : 6 * sc));
            nodeC.Children.Add(dot); nodeC.Children.Add(lb);
            cDots.Add(dot); cLbl.Add(lb);
        }

        // cincin + garis bidik di bintang yang sedang aktif
        cRing = new Canvas { RenderTransform = cRingT, Opacity = 0 };
        double rr = 23 * sc, tick = 34 * sc;
        var ring = new Ellipse { Width = rr * 2, Height = rr * 2, Stroke = Brushes.White, StrokeThickness = 1.5 };
        Canvas.SetLeft(ring, -rr); Canvas.SetTop(ring, -rr);
        cRing.Children.Add(ring);
        cRing.Children.Add(new Line { X1 = -tick, X2 = tick, Y1 = 0, Y2 = 0, Stroke = Brushes.White, StrokeThickness = 1.1, Opacity = 0.8 });
        cRing.Children.Add(new Line { X1 = 0, X2 = 0, Y1 = -tick, Y2 = tick, Stroke = Brushes.White, StrokeThickness = 1.1, Opacity = 0.8 });
        cBig = Tx(words[0], 46 * sc, FontWeights.Bold, Brushes.White, 0);
        cBig.TextWrapping = TextWrapping.NoWrap;
        cBig.Effect = new DropShadowEffect { Color = Accent, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.85 };
        nodeC.Children.Add(cRing); nodeC.Children.Add(cBig);

        var sub = Tx(line, 22 * sc, FontWeights.Normal, Brushes.White, 0.3);
        sub.Width = W * 0.7; sub.TextAlignment = TextAlignment.Center;
        Canvas.SetLeft(sub, W * 0.15); Canvas.SetTop(sub, bot + 74 * sc);
        nodeC.Children.Add(sub);

        rootC.Children.Add(lineC); rootC.Children.Add(nodeC);
        fxView.Children.Add(rootC);
        cShown = -1; cPos = cPts[0];
    }

    void FrameConstellation(double p, long now)
    {
        int cnt = cPts.Count;
        if (cnt == 0 || cRing == null || cBig == null) return;
        double t = now / 1000.0;
        double q = Math.Clamp(p / 0.88, 0, 1) * cnt;
        int n = Math.Min(cnt, (int)Math.Floor(q) + 1);
        double f = q >= cnt ? 1 : q - Math.Floor(q);

        if (n != cShown)
        {
            cShown = n;
            for (int i = 0; i < cnt; i++)
            {
                cDots[i].Opacity = i < n ? 1 : 0;
                cLbl[i].Opacity = i < n - 1 ? 0.85 : 0;
            }
            int ai = n - 1;
            cBig.Text = cWords[ai]; cBig.Opacity = 1;
            cBig.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double bw = cBig.DesiredSize.Width;
            cBigDx = cPts[ai].X + 18 * sc + bw > W - 40 * sc ? -(18 * sc + bw) : 18 * sc;
            cBigDy = 8 * sc;
        }

        foreach (var (ln, a, b) in cEdges)
            ln.Opacity = b >= n ? 0 : b < n - 1 ? 0.8 : 0.8 * Math.Min(1, f * 5);

        var tgt = cPts[n - 1];
        cPos = new Point(cPos.X + (tgt.X - cPos.X) * 0.2, cPos.Y + (tgt.Y - cPos.Y) * 0.2);
        cRingT.X = cPos.X; cRingT.Y = cPos.Y;
        cRing.Opacity = 0.75 + 0.25 * Math.Sin(t * 5);
        Canvas.SetLeft(cBig, cPos.X + cBigDx); Canvas.SetTop(cBig, cPos.Y + cBigDy);
        cDrift.X = Math.Sin(t * 0.25) * 14 * sc; cDrift.Y = Math.Cos(t * 0.21) * 8 * sc;
    }

    // ---------- 3. PER KATA: kata menyala satu per satu ----------
    readonly List<TextBlock> kWords = new();
    int kShown = -1;

    void BuildKaraoke(int idx, string line)
    {
        kWords.Clear(); kShown = -1;
        var words = Words(line);
        double fs = (words.Count > 16 ? 46 : words.Count > 10 ? 60 : 78) * sc;
        int perRow = Math.Max(8, (int)(W * 0.78 / (fs * 0.56)));
        var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var pv = DimLine(LineAt(idx - 1), 28 * sc, 0.26); pv.Margin = new Thickness(0, 0, 0, 28 * sc);
        st.Children.Add(pv);

        StackPanel NewRow() => new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var row = NewRow(); int chars = 0;
        foreach (var w in words)
        {
            if (chars + w.Length > perRow && row.Children.Count > 0) { st.Children.Add(row); row = NewRow(); chars = 0; }
            var tb = Tx(w, fs, FontWeights.Bold, Brushes.White, 0.2);
            tb.TextWrapping = TextWrapping.NoWrap;
            tb.Margin = new Thickness(fs * 0.13, 2, fs * 0.13, 2);
            tb.RenderTransformOrigin = new Point(0.5, 0.5); tb.RenderTransform = new ScaleTransform(1, 1);
            row.Children.Add(tb); kWords.Add(tb);
            chars += w.Length + 1;
        }
        st.Children.Add(row);
        var nx = DimLine(LineAt(idx + 1), 28 * sc, 0.26); nx.Margin = new Thickness(0, 28 * sc, 0, 0);
        st.Children.Add(nx);
        fxView.Children.Add(st);
    }

    void FrameKaraoke(double p)
    {
        int cnt = kWords.Count;
        if (cnt == 0) return;
        int n = Math.Min(cnt, (int)Math.Floor(Math.Clamp(p / 0.9, 0, 1) * cnt) + 1);
        if (n == kShown) return;
        kShown = n;
        for (int i = 0; i < n; i++)
        {
            var tb = kWords[i];
            tb.Opacity = 1;
            bool curW = i == n - 1;
            tb.Foreground = curW ? new SolidColorBrush(AccentHot) : Brushes.White;
            tb.Effect = curW ? new DropShadowEffect { Color = Accent, BlurRadius = 26, ShadowDepth = 0, Opacity = 0.9 } : null;
        }
        if (kWords[n - 1].RenderTransform is ScaleTransform sc2)
        {
            var anim = new DoubleAnimation(1.3, 1.0, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            sc2.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            sc2.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }
    }

    // ---------- 4. MESIN KETIK ----------
    TextBlock? twTyped;
    Run twCaret = new("▍");
    string twText = "";
    int twN = -1;

    void BuildTypewriter(int idx, string line)
    {
        double fs = 58 * sc * (line.Length > 70 ? 0.7 : line.Length > 45 ? 0.85 : 1);
        var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var pv = DimLine(LineAt(idx - 1), 26 * sc, 0.25); pv.FontFamily = Mono; pv.Margin = new Thickness(0, 0, 0, 34 * sc);
        st.Children.Add(pv);
        var g = new Grid { HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = W * 0.74 };
        var ghost = Tx(line, fs, FontWeights.Medium, Brushes.White, 0, Mono);
        var typ = Tx("", fs, FontWeights.Medium, Brushes.White, 1, Mono);
        ghost.LineHeight = typ.LineHeight = fs * 1.35;
        g.Children.Add(ghost); g.Children.Add(typ);
        st.Children.Add(g);
        twCaret = new Run("▍") { Foreground = caretBr };
        twTyped = typ; twText = line; twN = -1;
        fxView.Children.Add(st);
    }

    void FrameTypewriter(double p, long now)
    {
        if (twTyped == null) return;
        int len = twText.Length;
        int n = (int)Math.Ceiling(Math.Clamp(p / 0.78, 0, 1) * len);
        if (n != twN)
        {
            twN = n;
            twTyped.Inlines.Clear();
            twTyped.Inlines.Add(new Run(twText.Substring(0, n)));
            twTyped.Inlines.Add(twCaret);
        }
        twCaret.Foreground = n < len || ((now / 450) & 1) == 0 ? caretBr : Brushes.Transparent;
    }

    // ---------- 5. NEON: tulisan tabung neon yang berkedip ----------
    readonly SolidColorBrush neonBack = new(Colors.White), neonMid = new(Colors.White), neonCore = new(Colors.White);
    Grid? neonGrp;

    void BuildNeon(int idx, string line)
    {
        double fs = 82 * sc * (line.Length > 70 ? 0.62 : line.Length > 40 ? 0.8 : 1);
        var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var g = new Grid { HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = W * 0.8 };
        for (int k = 0; k < 3; k++)
        {
            var tb = Tx(line, fs, FontWeights.SemiBold, k == 0 ? neonBack : k == 1 ? neonMid : neonCore);
            tb.TextAlignment = TextAlignment.Center;
            if (k == 0) tb.Effect = new BlurEffect { Radius = 30 };
            else if (k == 1) tb.Effect = new BlurEffect { Radius = 9 };
            g.Children.Add(tb);
        }
        neonGrp = g;
        st.Children.Add(g);
        var nx = DimLine(LineAt(idx + 1), 28 * sc, 0.2); nx.Margin = new Thickness(0, 48 * sc, 0, 0);
        st.Children.Add(nx);
        fxView.Children.Add(st);
        UpdateNeonColors();
    }

    void UpdateNeonColors()
    {
        var a = Accent;
        neonBack.Color = a; neonMid.Color = Theme.Mix(a, Colors.White, 0.35); neonCore.Color = Theme.Mix(a, Colors.White, 0.82);
    }

    void FrameNeon(long now)
    {
        if (neonGrp == null) return;
        if (frame % 6 == 0) UpdateNeonColors();
        double ts = (now - fxStart) / 1000.0;
        double f = ts < 0.45 ? (ts > 0.08 && Math.Sin(ts * 70) > -0.2 ? 1 : 0.15) : Flicker(now / 1000.0);
        if (Math.Abs(neonGrp.Opacity - f) > 0.001) neonGrp.Opacity = f;
    }

    // ---------- 6. GELOMBANG: huruf bergelombang mengikuti suara ----------
    readonly List<TextBlock> wChars = new();
    readonly List<TranslateTransform> wTt = new();
    double wAmp;
    int wShown = -1;

    void BuildWave(int idx, string line)
    {
        wChars.Clear(); wTt.Clear(); wShown = -1;
        var parts = new List<string>();
        var en = StringInfo.GetTextElementEnumerator(line);
        while (en.MoveNext()) parts.Add((string)en.Current);
        double fs = Math.Min(76 * sc, W * 0.84 / (Math.Max(8, parts.Count) * 0.56));
        wAmp = fs * 0.16;
        var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var ch in parts)
        {
            var tb = Tx(ch, fs, FontWeights.Bold, Brushes.White, 0.28);
            tb.TextWrapping = TextWrapping.NoWrap;
            var tt = new TranslateTransform();
            tb.RenderTransform = tt;
            row.Children.Add(tb); wChars.Add(tb); wTt.Add(tt);
        }
        st.Children.Add(row);
        var nx = DimLine(LineAt(idx + 1), 28 * sc, 0.22); nx.Margin = new Thickness(0, 60 * sc, 0, 0);
        st.Children.Add(nx);
        fxView.Children.Add(st);
    }

    void FrameWave(double p, long now)
    {
        int cnt = wChars.Count;
        if (cnt == 0) return;
        double t = now / 1000.0;
        int n = Math.Min(cnt, (int)Math.Ceiling(Math.Clamp(p / 0.85, 0, 1) * cnt));
        if (n != wShown)
        {
            wShown = n;
            for (int i = 0; i < cnt; i++)
            {
                wChars[i].Opacity = i < n ? 1 : 0.28;
                wChars[i].Foreground = i == n - 1 ? new SolidColorBrush(AccentHot) : Brushes.White;
            }
        }
        double amp = wAmp * (0.4 + 0.9 * (snap.Playing ? level : 0));
        for (int i = 0; i < cnt; i++) wTt[i].Y = Math.Sin(t * 3.2 + i * 0.5) * amp;
    }

    // ---------- 7. GULIR: daftar lirik, baris aktif terang, lainnya buram ----------
    GradientStop sgB = new(Colors.White, 0), sgC = new(Color.FromArgb(0x66, 255, 255, 255), 0.04);

    void BuildScroll(int idx)
    {
        double hostW = W * 0.78, hostH = H - 300 * sc;
        var host = new Grid { Width = hostW, Height = hostH, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
        var mask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        mask.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
        mask.GradientStops.Add(new GradientStop(Colors.Black, 0.2));
        mask.GradientStops.Add(new GradientStop(Colors.Black, 0.8));
        mask.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        host.OpacityMask = mask;
        var tt = new TranslateTransform();
        var st = new StackPanel { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Width = hostW, RenderTransform = tt };

        var items = new List<(string text, int d)>();
        if (idx < 0)
        {
            items.Add(("♪", 0));
            for (int i = 0; i < 5; i++) if (lyr != null && i < lyr.Count) items.Add((lyr[i].l.Length == 0 ? "♪" : lyr[i].l, i + 1));
        }
        else
        {
            for (int i = idx - 3; i <= idx + 5; i++)
                if (lyr != null && i >= 0 && i < lyr.Count) items.Add((lyr[i].l.Length == 0 ? "♪" : lyr[i].l, i - idx));
        }

        int activeChild = 0;
        for (int k = 0; k < items.Count; k++)
        {
            var (text, d) = items[k];
            int ad = Math.Abs(d);
            bool act = d == 0;
            if (act) activeChild = k;
            var tb = Tx(text, (act ? 58 : 40) * sc, act ? FontWeights.Bold : FontWeights.SemiBold, Brushes.White, ad == 0 ? 1 : ad == 1 ? 0.5 : ad == 2 ? 0.3 : 0.18);
            tb.Margin = new Thickness(0, 12 * sc, 0, 12 * sc);
            tb.MaxWidth = hostW * 0.92; tb.HorizontalAlignment = HorizontalAlignment.Left; tb.TextAlignment = TextAlignment.Left;
            if (act)
            {
                var gb = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
                sgB = new GradientStop(Colors.White, 0); sgC = new GradientStop(Color.FromArgb(0x66, 255, 255, 255), 0.04);
                gb.GradientStops.Add(new GradientStop(Colors.White, 0));
                gb.GradientStops.Add(sgB); gb.GradientStops.Add(sgC);
                gb.GradientStops.Add(new GradientStop(Color.FromArgb(0x66, 255, 255, 255), 1));
                tb.Foreground = gb;
                tb.Effect = new DropShadowEffect { Color = Accent, BlurRadius = 30, ShadowDepth = 0, Opacity = 0.4 };
            }
            else tb.Effect = new BlurEffect { Radius = Math.Min(6, ad * 1.7) };
            st.Children.Add(tb);
        }

        st.Measure(new Size(hostW, double.PositiveInfinity));
        double topActive = 0, hActive = 0;
        for (int k = 0; k < st.Children.Count; k++)
        {
            double h = st.Children[k].DesiredSize.Height;
            if (k < activeChild) topActive += h;
            else if (k == activeChild) hActive = h;
        }
        double target = hostH / 2 - (topActive + hActive / 2);
        tt.Y = target;
        tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(target + 70 * sc, target, TimeSpan.FromMilliseconds(560)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        host.Children.Add(st);
        fxView.Children.Add(host);
    }
}
