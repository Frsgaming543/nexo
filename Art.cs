using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WPath = System.Windows.Shapes.Path;
namespace NexoIsland;

// Kostum Natal untuk ikon desktop = BINGKAI + aksesori di sekeliling ikon asli.
// Bagian tengah bingkai "dilubangi" (Clip) sehingga gambar aplikasi asli tetap terlihat persis, tidak digambar ulang.
// Ruang desain 100x100: ikon asli = kotak 0..100.
static class Art
{
    public const int Count = 11;

    public sealed class Costume
    {
        public Canvas Root = new() { Width = 100, Height = 100, IsHitTestVisible = false };
        public Canvas Plate = new() { Width = 100, Height = 100 };   // bingkai (berlubang di tengah)
        public Canvas Top = new() { Width = 100, Height = 100 };     // aksesori (topi, tanduk, dll) - ikut bergoyang
        public MatrixTransform TopMt = new();
        public List<UIElement> Blink = new();

        public Costume()
        {
            Plate.Clip = new CombinedGeometry(GeometryCombineMode.Exclude,
                new RectangleGeometry(new Rect(-80, -100, 260, 260)),
                new RectangleGeometry(new Rect(0, 0, 100, 100), 9, 9));
            Top.RenderTransform = TopMt;
            Root.Children.Add(Plate);
            Root.Children.Add(Top);
        }
    }

    // ---------- helper (dipakai juga oleh mainan) ----------
    static Color C(string h) => (Color)ColorConverter.ConvertFromString(h);
    internal static SolidColorBrush S(string h, double op = 1) { var b = new SolidColorBrush(C(h)) { Opacity = op }; b.Freeze(); return b; }

    internal static LinearGradientBrush LG(string a, string b)
    {
        var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        g.GradientStops.Add(new GradientStop(C(a), 0));
        g.GradientStops.Add(new GradientStop(C(b), 1));
        g.Freeze(); return g;
    }

    internal static RadialGradientBrush RG(string a, string b)
    {
        var g = new RadialGradientBrush { GradientOrigin = new Point(0.35, 0.3), Center = new Point(0.5, 0.5), RadiusX = 0.75, RadiusY = 0.75 };
        g.GradientStops.Add(new GradientStop(C(a), 0));
        g.GradientStops.Add(new GradientStop(C(b), 1));
        g.Freeze(); return g;
    }

    internal static Rectangle Rc(Canvas c, double x, double y, double w, double h, Brush? fill, double r = 0, Brush? stroke = null, double st = 0, DoubleCollection? dash = null)
    {
        var e = new Rectangle { Width = w, Height = h, Fill = fill, RadiusX = r, RadiusY = r, Stroke = stroke, StrokeThickness = st };
        if (dash != null) { e.StrokeDashArray = dash; e.StrokeDashCap = PenLineCap.Round; }
        Canvas.SetLeft(e, x); Canvas.SetTop(e, y); c.Children.Add(e); return e;
    }

    internal static Ellipse El(Canvas c, double x, double y, double w, double h, Brush? fill, Brush? stroke = null, double st = 0)
    {
        var e = new Ellipse { Width = w, Height = h, Fill = fill, Stroke = stroke, StrokeThickness = st };
        Canvas.SetLeft(e, x); Canvas.SetTop(e, y); c.Children.Add(e); return e;
    }

    internal static WPath Pa(Canvas c, string d, Brush? fill, Brush? stroke = null, double st = 0)
    {
        var p = new WPath
        {
            Data = Geometry.Parse(d), Fill = fill, Stroke = stroke, StrokeThickness = st,
            StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        };
        c.Children.Add(p); return p;
    }

    internal static string StarPath(double cx, double cy, double r)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 10; i++)
        {
            double ang = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 == 0 ? r : r * 0.45;
            sb.Append(i == 0 ? "M " : " L ");
            sb.Append(FormattableString.Invariant($"{cx + rr * Math.Cos(ang):0.##},{cy + rr * Math.Sin(ang):0.##}"));
        }
        return sb.Append(" Z").ToString();
    }

    static Rectangle Frame(Costume cs, Brush fill) => Rc(cs.Plate, -10, -10, 120, 116, fill, 26);

    public static Costume Build(int design)
    {
        var cs = new Costume();
        switch (design % Count)
        {
            case 0: Santa(cs); break;
            case 1: Bauble(cs); break;
            case 2: Gift(cs); break;
            case 3: Wreath(cs); break;
            case 4: Candy(cs); break;
            case 5: Globe(cs); break;
            case 6: Ginger(cs); break;
            case 7: Lights(cs); break;
            case 8: Reindeer(cs); break;
            case 9: Frost(cs); break;
            default: Elf(cs); break;
        }
        return cs;
    }

    static void Bow(Canvas t, double cx, double y, string a, string b)
    {
        var f = LG(a, b);
        Pa(t, FormattableString.Invariant($"M {cx},{y} C {cx - 28},{y - 34} {cx - 52},{y - 6} {cx},{y + 4} Z"), f, S("#000000", 0.3), 1);
        Pa(t, FormattableString.Invariant($"M {cx},{y} C {cx + 28},{y - 34} {cx + 52},{y - 6} {cx},{y + 4} Z"), f, S("#000000", 0.3), 1);
        El(t, cx - 8, y - 6, 16, 13, S(b), S("#000000", 0.3), 1);
    }

    // 0. Santa: bingkai merah, bulu putih, topi Santa
    static void Santa(Costume cs)
    {
        Frame(cs, LG("#E5473F", "#A31E24"));
        Rc(cs.Plate, -3, -3, 106, 106, null, 12, S("#FFFFFF"), 6);
        var t = cs.Top;
        Pa(t, "M 2,-17 C 6,-50 36,-64 66,-48 C 70,-42 66,-24 64,-17 Z", S("#D93636"), S("#8E1B1B"), 1.5);
        Rc(t, -2, -18, 70, 12, S("#FFFFFF"), 6, S("#E3E7F0"), 1.5);
        El(t, 58, -62, 18, 18, S("#FFFFFF"), S("#E3E7F0"), 1.5);
    }

    // 1. Bauble: bingkai bola Natal mengilap + tutup emas
    static void Bauble(Costume cs)
    {
        Frame(cs, RG("#FF6A6A", "#B3121B"));
        Rc(cs.Plate, -2, -2, 104, 104, null, 12, S("#FFD866"), 3);
        var rr = new Random(3);
        for (int i = 0; i < 18; i++) El(cs.Plate, -8 + rr.NextDouble() * 112, -8 + rr.NextDouble() * 112, 2.5, 2.5, S("#FFE9A8", 0.9));
        var t = cs.Top;
        Rc(t, 36, -26, 28, 14, LG("#F6D56B", "#C79A25"), 3);
        El(t, 43, -46, 14, 22, null, S("#D8B13A"), 3);
    }

    // 2. Gift: bingkai kado hijau + pita emas + bow
    static void Gift(Costume cs)
    {
        Frame(cs, LG("#34B466", "#18753F"));
        var gold = LG("#F8DA6A", "#D9A92B");
        Rc(cs.Plate, 42, -10, 16, 10, gold); Rc(cs.Plate, 42, 100, 16, 6, gold);
        Rc(cs.Plate, -10, 42, 10, 16, gold); Rc(cs.Plate, 100, 42, 10, 16, gold);
        Bow(cs.Top, 50, -10, "#FFE27A", "#E0B030");
    }

    // 3. Wreath: karangan daun + beri merah + pita
    static void Wreath(Costume cs)
    {
        Frame(cs, S("#1B5E32"));
        Rc(cs.Plate, -5, -5, 110, 110, null, 20, S("#2FA457"), 6);
        for (int k = 0; k < 6; k++)
        {
            double v = 8 + k * 17;
            El(cs.Plate, v - 3.5, -7.5, 7, 7, S("#E5353B")); El(cs.Plate, v - 3.5, 100.5, 7, 7, S("#E5353B"));
            El(cs.Plate, -7.5, v - 3.5, 7, 7, S("#E5353B")); El(cs.Plate, 100.5, v - 3.5, 7, 7, S("#E5353B"));
        }
        Bow(cs.Top, 50, -10, "#E5473F", "#B82727");
    }

    // 4. Candy cane: bingkai garis merah-putih + bintang emas
    static void Candy(Costume cs)
    {
        var stripes = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute, SpreadMethod = GradientSpreadMethod.Repeat,
            StartPoint = new Point(0, 0), EndPoint = new Point(18, 18)
        };
        stripes.GradientStops.Add(new GradientStop(C("#D93636"), 0));
        stripes.GradientStops.Add(new GradientStop(C("#D93636"), 0.5));
        stripes.GradientStops.Add(new GradientStop(C("#FFFFFF"), 0.5));
        stripes.GradientStops.Add(new GradientStop(C("#FFFFFF"), 1));
        stripes.Freeze();
        Frame(cs, stripes);
        Pa(cs.Top, StarPath(100, -8, 15), LG("#FFE27A", "#F0B429"), S("#B98A16"), 1);
    }

    // 5. Snow globe: bingkai kaca biru + alas merah + daun holly
    static void Globe(Costume cs)
    {
        Frame(cs, RG("#EEF7FF", "#86B5E5"));
        Rc(cs.Plate, -6, 98, 112, 8, LG("#B33A3A", "#7A1F1F"), 3);
        var rr = new Random(7);
        for (int i = 0; i < 16; i++) El(cs.Plate, -8 + rr.NextDouble() * 112, -8 + rr.NextDouble() * 104, 3, 3, S("#FFFFFF", 0.95));
        Pa(cs.Plate, "M -4,26 Q -6,-4 26,-6", null, S("#FFFFFF", 0.85), 4);
        var t = cs.Top; var g = S("#2E8B4F");
        Pa(t, "M -8,-6 Q 0,-26 14,-8 Q 4,-4 -8,-6 Z", g, S("#14592B"), 1);
        Pa(t, "M 8,-8 Q 20,-24 30,-6 Q 18,-2 8,-8 Z", g, S("#14592B"), 1);
        El(t, -2, -12, 8, 8, S("#E5353B")); El(t, 6, -10, 8, 8, S("#E5353B")); El(t, 2, -5, 8, 8, S("#C21F2B"));
    }

    // 6. Gingerbread: bingkai kue jahe + icing + gumdrop
    static void Ginger(Costume cs)
    {
        Frame(cs, LG("#BE7A40", "#87491F"));
        Rc(cs.Plate, -3, -3, 106, 106, null, 13, S("#FFF4E2"), 3.2, new DoubleCollection { 0.2, 1.6 });
        var t = cs.Top;
        El(t, -14, -14, 16, 16, RG("#FF6B6B", "#C21F2B"));
        El(t, 98, -14, 16, 16, RG("#6BD68A", "#1E8A3E"));
        El(t, -14, 98, 16, 16, RG("#6BB6FF", "#1F5FB8"));
        El(t, 98, 98, 16, 16, RG("#FFE27A", "#D99A18"));
    }

    // 7. Lights: bingkai malam + untaian lampu berkedip
    static void Lights(Costume cs)
    {
        Frame(cs, LG("#1B2A55", "#0B1128"));
        var t = cs.Top;
        Pa(t, "M -10,-24 Q 50,-4 110,-24", null, S("#2E7D4F"), 2);
        string[] cols = { "#FF5A5A", "#FFD24A", "#5AD0FF", "#7CFF8A", "#FF8AD8" };
        double[] ts = { 0.1, 0.3, 0.5, 0.7, 0.9 };
        for (int i = 0; i < 5; i++)
        {
            double u = ts[i], x = -10 + 120 * u, y = -24 + 40 * u * (1 - u);
            cs.Blink.Add(El(t, x - 4.5, y + 1, 9, 12, S(cols[i]), S("#FFFFFF", 0.6), 0.8));
        }
        for (int i = 0; i < 5; i++)
            cs.Blink.Add(El(cs.Plate, -2 + i * 24, 98, 8, 8, S(cols[(i + 2) % 5])));
    }

    // 8. Reindeer: tanduk, telinga, hidung merah
    static void Reindeer(Costume cs)
    {
        Frame(cs, LG("#A5723F", "#6B4122"));
        var t = cs.Top; var br = S("#6B4122");
        Pa(t, "M 26,-8 L 20,-30 M 23,-19 L 9,-27 M 20,-30 L 22,-44", null, br, 6);
        Pa(t, "M 74,-8 L 80,-30 M 77,-19 L 91,-27 M 80,-30 L 78,-44", null, br, 6);
        El(t, -18, 8, 22, 14, S("#8A5A30")); El(t, 96, 8, 22, 14, S("#8A5A30"));
        El(t, 39, 91, 22, 20, RG("#FF6A5A", "#C21F1F"), S("#8E1B1B"), 1.2);
    }

    // 9. Frost: bingkai biru es + tumpukan salju + es menggantung
    static void Frost(Costume cs)
    {
        Frame(cs, LG("#3566B0", "#17356E"));
        var rr = new Random(11);
        for (int i = 0; i < 14; i++) El(cs.Plate, -8 + rr.NextDouble() * 112, -8 + rr.NextDouble() * 112, 2.5, 2.5, S("#FFFFFF", 0.85));
        var t = cs.Top;
        Pa(t, "M -12,2 Q 0,-22 22,-14 Q 38,-28 58,-16 Q 76,-26 90,-14 Q 104,-20 112,2 Z", S("#FFFFFF"), S("#D3E4F8"), 1.5);
        var ice = S("#E4F1FF");
        Pa(t, "M 8,0 L 16,0 L 12,12 Z", ice); Pa(t, "M 36,0 L 44,0 L 40,9 Z", ice);
        Pa(t, "M 66,0 L 74,0 L 70,13 Z", ice); Pa(t, "M 88,0 L 94,0 L 91,8 Z", ice);
    }

    // 10. Elf: bingkai hijau + zigzag merah + topi elf berlonceng
    static void Elf(Costume cs)
    {
        Frame(cs, LG("#3FB062", "#1C7B3C"));
        var sb = new StringBuilder("M -8,98");
        for (int x = -8; x < 104; x += 8) sb.Append($" L {x + 4},106 L {x + 8},98");
        Pa(cs.Plate, sb.ToString(), S("#D93A3A"));
        var t = cs.Top;
        Pa(t, "M 2,-8 C 8,-40 46,-52 92,-38 C 98,-36 100,-28 96,-26 C 70,-30 64,-14 62,-8 Z", S("#2E9E52"), S("#14592B"), 1.5);
        Rc(t, -2, -16, 66, 10, S("#D93A3A"), 5, S("#8E1B1B"), 1.2);
        El(t, 90, -44, 14, 14, RG("#FFE27A", "#D99A18"), S("#B98A16"), 1);
    }
}
