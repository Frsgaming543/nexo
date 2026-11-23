using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WPath = System.Windows.Shapes.Path;
namespace NexoIsland;

// Satu bola lampu = inti kecil + cahaya (glow) lembut di sekelilingnya
class Bulb { public Ellipse Core = null!, Glow = null!; public int Idx; public string Applied = ""; }

// Warna & pola nyala lampu hias (dibaca dari Pengaturan, berlaku untuk lampu pohon dan lampu jendela)
static class LightStyle
{
    static readonly Dictionary<string, (Brush core, Brush glow)> cache = new();

    public static string[] Palette(string name) => name switch
    {
        "warm" => new[] { "#FFC857", "#FFB347", "#FFD98A" },
        "cool" => new[] { "#9FD8FF", "#FFFFFF", "#6AB7FF" },
        "pink" => new[] { "#FF8AD8", "#FF5FA2", "#FFC4EA" },
        "redgreen" => new[] { "#FF4B4B", "#3DDC6B" },
        _ => new[] { "#FF5A5A", "#FFD24A", "#5AD0FF", "#7CFF8A", "#FF8AD8" }
    };

    static string Hex(int idx) { var p = Palette(Prefs.Current.LightColor); return p[idx % p.Length]; }

    static (Brush core, Brush glow) For(string hex)
    {
        if (cache.TryGetValue(hex, out var b)) return b;
        var col = (Color)ColorConverter.ConvertFromString(hex);
        var core = Art.RG("#FFFFFF", hex);
        var glow = new RadialGradientBrush();
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(215, col.R, col.G, col.B), 0));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(90, col.R, col.G, col.B), 0.45));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, col.R, col.G, col.B), 1));
        glow.Freeze();
        cache[hex] = (core, glow);
        return cache[hex];
    }

    public static Bulb MakeBulb(Canvas host, double x, double y, int idx)
    {
        var b = new Bulb { Idx = idx };
        b.Glow = new Ellipse { Width = 17, Height = 17, IsHitTestVisible = false };
        b.Core = new Ellipse { Width = 5.4, Height = 5.4, IsHitTestVisible = false };
        host.Children.Add(b.Glow); host.Children.Add(b.Core);
        Place(b, x, y);
        return b;
    }

    public static void Place(Bulb b, double x, double y)
    {
        Canvas.SetLeft(b.Glow, x - 8.5); Canvas.SetTop(b.Glow, y - 8.5);
        Canvas.SetLeft(b.Core, x - 2.7); Canvas.SetTop(b.Core, y - 2.7);
    }

    // tingkat terang 0..1 menurut pola
    static double Level(string pattern, int idx, double t, double speed)
    {
        switch (pattern)
        {
            case "steady": return 1;
            case "blink": return (((int)Math.Floor(t * speed * 1.6) + idx) & 1) == 0 ? 1 : 0.15;
            case "chase":
            {
                double f = (t * speed * 5 - idx * 0.8) % 4; if (f < 0) f += 4;
                return f < 1.3 ? 1 : 0.15;
            }
            default: return 0.55 + 0.45 * Math.Sin(t * speed * 2.2 + idx * 2.399);
        }
    }

    public static void Paint(Bulb b, double t)
    {
        var p = Prefs.Current;
        string hex = Hex(b.Idx);
        if (b.Applied != hex)
        {
            var br = For(hex);
            b.Core.Fill = br.core; b.Glow.Fill = br.glow; b.Applied = hex;
        }
        double lv = Level(p.LightPattern, b.Idx, t, Math.Clamp(p.LightSpeed, 0.2, 4));
        b.Glow.Opacity = lv;
        b.Core.Opacity = 0.4 + 0.6 * lv;
    }
}

// Kabel lampu hias yang terpasang di tepi atas jendela (ikut bergerak bersama jendela)
public partial class SnowWindow
{
    class Strand
    {
        public IntPtr H; public Canvas Host = null!; public WPath Wire = null!;
        public TranslateTransform Tt = new(); public List<Bulb> Bulbs = new(); public string Sig = "";
    }

    readonly Canvas lightLayer = new();
    readonly Dictionary<IntPtr, Strand> strands = new();
    bool strandHot, wasRDownS;

    void AttachStrand(Cap c)
    {
        if (strands.ContainsKey(c.H)) return;
        var s = new Strand { H = c.H };
        s.Host = new Canvas { IsHitTestVisible = false, RenderTransform = s.Tt };
        s.Wire = new WPath { Stroke = Solid("#1E5A38"), StrokeThickness = 1.6, IsHitTestVisible = false };
        s.Host.Children.Add(s.Wire);
        lightLayer.Children.Add(s.Host);
        strands[c.H] = s;
        ApplyPrefs();
    }

    void RemoveStrand(IntPtr key)
    {
        if (strands.TryGetValue(key, out var s)) { lightLayer.Children.Remove(s.Host); strands.Remove(key); }
        if (!HasLights) ApplyPrefs();
    }

    void ClearStrands()
    {
        ClearLinks();
        foreach (var k in strands.Keys.ToList()) { lightLayer.Children.Remove(strands[k].Host); strands.Remove(k); }
        ApplyPrefs();
    }

    static double WireY(double x, double a, double swag, double sag)
    {
        double u = ((x - a) % swag) / swag;
        return 1 + sag * 4 * u * (1 - u);
    }

    void RebuildStrand(Strand s, Cap cap, AppSettings p)
    {
        double sp = Math.Clamp(p.LightSpacing, 12, 80), sag = Math.Clamp(p.LightSag, 0, 50), swag = sp * 4;
        var g = new StreamGeometry();
        var pos = new List<Point>();
        using (var ctx = g.Open())
        {
            foreach (var (a0, b0) in cap.Segs)
            {
                double a = a0 - cap.Left + 4, b = b0 - cap.Left - 4;
                if (b - a < 12) continue;
                ctx.BeginFigure(new Point(a, WireY(a, a, swag, sag)), false, false);
                var pts = new List<Point>();
                for (double x = a + 3; x < b; x += 3) pts.Add(new Point(x, WireY(x, a, swag, sag)));
                pts.Add(new Point(b, WireY(b, a, swag, sag)));
                ctx.PolyLineTo(pts, true, true);
                for (double x = a + sp * 0.5; x < b && pos.Count < 320; x += sp) pos.Add(new Point(x, WireY(x, a, swag, sag) + 3));
            }
        }
        g.Freeze();
        s.Wire.Data = g;

        while (s.Bulbs.Count < pos.Count) s.Bulbs.Add(LightStyle.MakeBulb(s.Host, 0, 0, s.Bulbs.Count));
        while (s.Bulbs.Count > pos.Count)
        {
            var last = s.Bulbs[s.Bulbs.Count - 1];
            s.Host.Children.Remove(last.Glow); s.Host.Children.Remove(last.Core);
            s.Bulbs.RemoveAt(s.Bulbs.Count - 1);
        }
        for (int i = 0; i < pos.Count; i++) LightStyle.Place(s.Bulbs[i], pos[i].X, pos[i].Y);
    }

    // dipanggil setiap frame setelah UpdateCaps
    void UpdateStrands(double cx, double cy, bool rdown)
    {
        bool rclick = rdown && !wasRDownS; wasRDownS = rdown;
        strandHot = false;
        if (strands.Count == 0) return;

        var p = Prefs.Current;
        double t = clock.Elapsed.TotalSeconds;
        bool shift = (Native.GetAsyncKeyState(0x10) & 0x8000) != 0;
        Strand? hover = null;

        foreach (var key in strands.Keys.ToList())
        {
            var s = strands[key];
            if (!caps.TryGetValue(key, out var cap) || cap.Segs.Count == 0)
            {
                if (!Desk.Alive(key)) RemoveStrand(key);          // jendela sudah ditutup
                else s.Host.Visibility = Visibility.Collapsed;     // diminimize / tertutup jendela lain
                continue;
            }
            s.Host.Visibility = Visibility.Visible;
            s.Tt.X = cap.Left; s.Tt.Y = cap.Top;

            string sig = FormattableString.Invariant($"{cap.Width:0}|{p.LightSpacing:0.#}|{p.LightSag:0.#}|")
                         + string.Join(",", cap.Segs.Select(g => FormattableString.Invariant($"{g.a - cap.Left:0}-{g.b - cap.Left:0}")));
            if (sig != s.Sig) { s.Sig = sig; RebuildStrand(s, cap, p); }

            if (frame % 2 == 0) foreach (var b in s.Bulbs) LightStyle.Paint(b, t);

            // Shift + klik kanan di kabel = lepas lampu dari jendela itu
            if (shift && hover == null && cx >= cap.Left && cx <= cap.Left + cap.Width && cy >= cap.Top - 14 && cy <= cap.Top + p.LightSag + 18) hover = s;
        }

        strandHot = hover != null;
        if (hover != null && rclick)
        {
            Spray(cx, cy, 8, 3, true, Gold);
            RemoveStrand(hover.H);
            strandHot = false;
        }
    }
}
