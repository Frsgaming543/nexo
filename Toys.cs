using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace SnowIsland;

enum TK { Dozer, Sleigh, BallS, BallM, BallL, Eyes, Nose, Hat, Scarf, Arms, Buttons, Tree, Gift, Strand }

// Jembatan Pengaturan -> SnowWindow: ToyBox.Add("dozer") dst.
static class ToyBox
{
    public static event Action<string>? Spawn;
    public static void Add(string kind) => Spawn?.Invoke(kind);
}

// Gambar vektor semua mainan. Titik (0,0) = tengah-bawah (mainan) / titik pasang (aksesori manusia salju).
static class ToyArt
{
    public static double Radius(TK k) => k == TK.BallS ? 24 : k == TK.BallM ? 34 : k == TK.BallL ? 46 : 0;
    public static bool IsBall(TK k) => k == TK.BallS || k == TK.BallM || k == TK.BallL;
    public static bool IsAcc(TK k) => k >= TK.Eyes && k <= TK.Buttons;

    // jarak titik pasang ke dasar saat tergeletak (px) dan posisi pasang di bola radius 32
    public static double Foot(TK k) => k switch { TK.Eyes => 8, TK.Nose => 6, TK.Scarf => 8, TK.Arms => 6, TK.Buttons => 20, _ => 0 };
    public static Point Snap(TK k) => k switch
    {
        TK.Eyes => new Point(0, -10), TK.Nose => new Point(3, 0), TK.Hat => new Point(0, -28),
        TK.Scarf => new Point(0, 23), TK.Arms => new Point(0, 4), _ => new Point(0, 8)
    };

    public static Rect Bounds(TK k) => k switch
    {
        TK.Dozer => new Rect(-62, -110, 135, 110), TK.Sleigh => new Rect(-92, -118, 235, 118),
        TK.Eyes => new Rect(-18, -10, 36, 20), TK.Nose => new Rect(-8, -8, 26, 16), TK.Hat => new Rect(-26, -38, 52, 38),
        TK.Scarf => new Rect(-30, -8, 60, 40), TK.Arms => new Rect(-56, -26, 112, 34), TK.Buttons => new Rect(-9, -20, 18, 40),
        TK.Tree => new Rect(-92, -284, 184, 284), TK.Gift => new Rect(-24, -46, 48, 46), TK.Strand => new Rect(-17, -32, 40, 32), _ => new Rect(-10, -10, 20, 20)
    };

    // bola salju dengan radius bebas (titik 0,0 = tengah-bawah)
    public static Canvas BallVisual(double r)
    {
        var c = new Canvas { IsHitTestVisible = false };
        Art.El(c, -r, -2 * r, 2 * r, 2 * r, Art.RG("#FFFFFF", "#BFD5F0"), Art.S("#A9C4E6"), 1.5);
        Art.Pa(c, FormattableString.Invariant($"M {-r * 0.55:0.##},{-r * 1.25:0.##} Q 0,{-r * 1.75:0.##} {r * 0.55:0.##},{-r * 1.25:0.##}"), null, Art.S("#B7CDEB"), Math.Max(1.2, r * 0.05));
        Art.Pa(c, FormattableString.Invariant($"M {-r * 0.5:0.##},{-r * 0.6:0.##} Q {r * 0.1:0.##},{-r * 0.2:0.##} {r * 0.7:0.##},{-r * 0.7:0.##}"), null, Art.S("#B7CDEB"), Math.Max(1.2, r * 0.05));
        Art.El(c, -r * 0.55, -2 * r + r * 0.2, r * 0.5, r * 0.3, Art.S("#FFFFFF", 0.7));
        return c;
    }

    public static (Canvas c, List<UIElement> blink) Build(TK k)
    {
        var c = new Canvas { IsHitTestVisible = false };
        var blink = new List<UIElement>();
        var S = (string h, double o) => Art.S(h, o);
        switch (k)
        {
            case TK.BallS: case TK.BallM: case TK.BallL:
                return (BallVisual(Radius(k)), blink);
            case TK.Eyes:
                Art.El(c, -13, -4, 8, 9, S("#1D1D1D", 1)); Art.El(c, 5, -4, 8, 9, S("#1D1D1D", 1));
                Art.El(c, -11, -3, 3, 3, S("#FFFFFF", 0.8)); Art.El(c, 7, -3, 3, 3, S("#FFFFFF", 0.8));
                break;
            case TK.Nose:
                Art.Pa(c, "M -4,-3 L 17,2 L -4,7 Z", S("#F28C28", 1), S("#B85F10", 1), 1);
                Art.Pa(c, "M 2,-1 L 3,5 M 8,0 L 8,4", null, S("#B85F10", 1), 1);
                break;
            case TK.Hat:
                Art.Rc(c, -15, -36, 30, 29, S("#1F1F25", 1), 3);
                Art.Rc(c, -24, -7, 48, 7, S("#1F1F25", 1), 3);
                Art.Rc(c, -15, -15, 30, 7, S("#C23B3B", 1));
                break;
            case TK.Scarf:
                Art.Rc(c, -28, -6, 56, 12, Art.LG("#E5473F", "#B02A2A"), 6);
                Art.Rc(c, 10, 2, 11, 28, Art.LG("#E5473F", "#B02A2A"), 3);
                Art.Rc(c, -14, -6, 5, 12, S("#FFFFFF", 0.9)); Art.Rc(c, 2, -6, 5, 12, S("#FFFFFF", 0.9));
                Art.Rc(c, 10, 18, 11, 4, S("#FFFFFF", 0.9));
                break;
            case TK.Arms:
                Art.Pa(c, "M -27,0 L -52,-14 M -42,-8 L -50,-2 M -46,-11 L -48,-22", null, S("#6B4122", 1), 3.5);
                Art.Pa(c, "M 27,0 L 52,-14 M 42,-8 L 50,-2 M 46,-11 L 48,-22", null, S("#6B4122", 1), 3.5);
                break;
            case TK.Buttons:
                Art.El(c, -4, -16, 8, 8, S("#1F1F25", 1)); Art.El(c, -4, -4, 8, 8, S("#1F1F25", 1)); Art.El(c, -4, 8, 8, 8, S("#1F1F25", 1));
                break;
            case TK.Tree: BuildTree(c, blink); break;
            case TK.Strand:
            {
                Art.El(c, -15, -30, 30, 30, null, Art.S("#2E7D4F"), 2.2);
                Art.El(c, -10, -25, 20, 20, null, Art.S("#2E7D4F"), 2.2);
                Art.Rc(c, 13, -15, 10, 8, Art.S("#E8E8E8"), 2);
                string[] sc = { "#FF5A5A", "#FFD24A", "#5AD0FF", "#7CFF8A", "#FF8AD8", "#FFD24A" };
                for (int i = 0; i < sc.Length; i++)
                {
                    double ang = i * Math.PI / 3;
                    blink.Add(Art.El(c, Math.Cos(ang) * 15 - 4, -15 + Math.Sin(ang) * 15 - 4, 8, 8, Art.S(sc[i], 1), Art.S("#FFFFFF", 0.6), 0.8));
                }
                break;
            }
            case TK.Gift:
            {
                Art.Rc(c, -22, -34, 44, 34, Art.LG("#E5473F", "#A31E24"), 3);
                Art.Rc(c, -4, -34, 8, 34, Art.LG("#F8DA6A", "#D9A92B"));
                Art.Rc(c, -22, -22, 44, 8, Art.LG("#F8DA6A", "#D9A92B"));
                Art.Pa(c, "M 0,-34 C -14,-54 -30,-38 0,-32 Z", Art.LG("#FFE27A", "#E0B030"), S("#B98A16", 1), 1);
                Art.Pa(c, "M 0,-34 C 14,-54 30,-38 0,-32 Z", Art.LG("#FFE27A", "#E0B030"), S("#B98A16", 1), 1);
                break;
            }
            case TK.Dozer: BuildDozer(c); break;
            default: BuildSleigh(c); break;
        }
        return (c, blink);
    }

    static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    // Pohon Natal besar (tinggi ~280 px): 6 tingkat cabang bergerigi & menjuntai, bayangan, tekstur ranting, salju di ujung cabang,
    // bola hias, bintang. Elemen terakhir = grup lampu hias (tersembunyi sampai gulungan lampu dilepas di dekat pohon).
    static void BuildTree(Canvas c, List<UIElement> blink)
    {
        var rr = new Random(11);
        // batang
        Art.Rc(c, -11, -30, 22, 30, Art.LG("#8A5A32", "#4E2F17"), 2);
        Art.Pa(c, "M -5,-30 L -6,0 M 2,-26 L 3,0 M 7,-28 L 7,-2", null, Art.S("#2E1A0B", 0.55), 1.2);
        // tingkat: (dasar y, tinggi, setengah lebar) dari bawah ke atas
        double[][] T =
        {
            new[] { -22.0, 84, 88 }, new[] { -62.0, 80, 76 }, new[] { -100.0, 76, 64 },
            new[] { -136.0, 70, 52 }, new[] { -170.0, 64, 41 }, new[] { -202.0, 58, 29 }
        };
        string[][] bc = { new[] { "#FF6B6B", "#A31E24" }, new[] { "#FFD24A", "#B8860B" }, new[] { "#6AD7FF", "#1E6FA8" }, new[] { "#FF9AE0", "#B02E86" }, new[] { "#9BFF9B", "#2E8B3E" } };
        int bi = 0;
        var swags = new List<double[]>();   // (yy, hwT, droop) per tingkat
        for (int i = 0; i < T.Length; i++)
        {
            double yb = T[i][0], h = T[i][1], hw = T[i][2], apex = yb - h;
            // bayangan di bawah tingkat ini (jatuh ke tingkat bawahnya)
            Art.El(c, -hw * 0.96, yb - 6, hw * 1.92, 16, Art.S("#031A0A", 0.38));
            var sb = new StringBuilder();
            sb.Append("M 0," + F(apex));
            const int n = 7;
            for (int k = 1; k <= n; k++)
            {
                double t = k / (double)n, jit = (k % 2 == 1) ? -hw * 0.06 : hw * 0.015;
                sb.Append(" L " + F(-hw * t + jit) + "," + F(apex + h * t));
            }
            int m = Math.Max(4, (int)(hw / 13));
            var edge = new List<(double x, double y)>();
            for (int j = 0; j < m; j++)
            {
                double x0 = -hw + 2 * hw * j / m, x1 = -hw + 2 * hw * (j + 1) / m, mid = (x0 + x1) / 2;
                double droop = 9 + rr.NextDouble() * 7;
                sb.Append(" Q " + F(mid) + "," + F(yb + droop) + " " + F(x1) + "," + F(yb - 1 + rr.NextDouble() * 3));
                edge.Add((mid, yb));
            }
            for (int k = n - 1; k >= 0; k--)
            {
                double t = k / (double)n, jit = (k % 2 == 1) ? hw * 0.06 : -hw * 0.015;
                sb.Append(" L " + F(hw * t + jit) + "," + F(apex + h * t));
            }
            sb.Append(" Z");
            Art.Pa(c, sb.ToString(), Art.LG("#3BB066", "#0C4A25"), Art.S("#082E16"), 1);

            // tekstur ranting: garis gelap & terang menyebar dari tengah ke ujung cabang
            var br = new StringBuilder(); var hl = new StringBuilder();
            for (int j = 0; j <= 8; j++)
            {
                double ex = -hw * 0.92 + 1.84 * hw * j / 8, ey = yb - 4 + (j % 2) * 3;
                br.Append("M 0," + F(apex + h * 0.4) + " L " + F(ex) + "," + F(ey) + " ");
                hl.Append("M " + F(ex * 0.15 + 2) + "," + F(apex + h * 0.45) + " L " + F(ex * 0.9 + 3) + "," + F(ey - 3) + " ");
            }
            Art.Pa(c, br.ToString(), null, Art.S("#052A14", 0.30), 1.4);
            Art.Pa(c, hl.ToString(), null, Art.S("#8CF0AA", 0.13), 1.2);

            // salju di ujung cabang bawah + di sisi miring
            foreach (var (ex, ey) in edge)
                if (rr.NextDouble() < 0.75)
                {
                    double w = 11 + rr.NextDouble() * 9;
                    Art.El(c, ex - w / 2, ey - 7, w, 7 + rr.NextDouble() * 3, Art.S("#FFFFFF", 0.93));
                    Art.El(c, ex - w / 2 + 2, ey - 3, w * 0.7, 3.5, Art.S("#CFE0F7", 0.7));
                }
            for (int s2 = 0; s2 < 3; s2++)
            {
                double t = 0.35 + rr.NextDouble() * 0.5, sx = (s2 % 2 == 0 ? -1 : 1) * hw * t * 0.8;
                Art.El(c, sx - 6, apex + h * t - 3, 12 + rr.NextDouble() * 6, 5, Art.S("#FFFFFF", 0.85));
            }

            // bola hias
            for (int q = 0; q < 3; q++)
            {
                double yy = apex + h * (0.5 + rr.NextDouble() * 0.38), t = (yy - apex) / h, xx = (rr.NextDouble() * 2 - 1) * hw * t * 0.75;
                var col = bc[bi++ % bc.Length];
                Art.El(c, xx - 5, yy - 5, 10, 10, Art.RG(col[0], col[1]), Art.S("#FFFFFF", 0.35), 0.6);
                Art.El(c, xx - 2.5, yy - 3.5, 3, 3, Art.S("#FFFFFF", 0.85));
            }
            swags.Add(new[] { apex, hw, h });
        }
        // bintang + cahaya
        blink.Add(Art.El(c, -26, -294, 52, 52, Art.RG("#FFF6BF", "#00FFE27A")));
        Art.Pa(c, Art.StarPath(0, -268, 14), Art.LG("#FFE27A", "#F0B429"), Art.S("#B98A16"), 1);

        // grup lampu hias (elemen TERAKHIR)
        var lit = new Canvas { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        var bulbs = new List<Bulb>();
        for (int i = 0; i < T.Length; i++)
        {
            double yb = T[i][0], h = T[i][1], hw = T[i][2], apex = yb - h;
            for (int sw = 0; sw < 2; sw++)
            {
                double t = sw == 0 ? 0.40 : 0.80, yy = apex + h * t, hwT = hw * t * 0.88, droop = sw == 0 ? 9 : 14;
                Art.Pa(lit, "M " + F(-hwT) + "," + F(yy) + " Q 0," + F(yy + droop * 2) + " " + F(hwT) + "," + F(yy), null, Art.S("#0A2A16", 0.95), 1.4);
                int nb = Math.Max(3, (int)(hwT * 2 / 15));
                for (int k = 0; k < nb; k++)
                {
                    double u = (k + 0.5) / nb, iu = 1 - u;
                    double bx = iu * iu * -hwT + u * u * hwT;
                    double by = iu * iu * yy + 2 * iu * u * (yy + droop * 2) + u * u * yy;
                    bulbs.Add(LightStyle.MakeBulb(lit, bx, by + 3, bulbs.Count));
                }
            }
        }
        c.Children.Add(lit);
        c.Tag = bulbs;
    }

    // Buldoser Natal (menghadap kanan; sisi depan = pisau di x ~ +70)
    static void BuildDozer(Canvas c)
    {
        Art.Rc(c, -52, -24, 104, 24, Art.S("#2B2B2B"), 12);
        for (int i = 0; i < 5; i++)
        {
            Art.El(c, -44 + i * 21, -21, 18, 18, Art.S("#4A4A4A"), Art.S("#777777"), 1.5);
            Art.El(c, -39 + i * 21, -16, 8, 8, Art.S("#9A9A9A"));
        }
        Art.Rc(c, -40, -52, 78, 30, Art.LG("#E5473F", "#B02A2A"), 6);
        Art.Rc(c, -40, -40, 78, 5, Art.S("#FFFFFF", 0.9));
        Art.Rc(c, -38, -80, 36, 30, Art.LG("#E5473F", "#B02A2A"), 5);
        Art.Rc(c, -33, -75, 26, 16, Art.S("#BFE3FF"), 3);
        Art.Rc(c, -42, -84, 44, 6, Art.S("#8E1B1B"), 3);
        Art.Rc(c, 12, -68, 7, 18, Art.S("#444444"));
        Art.Pa(c, "M -36,-84 C -34,-104 -14,-110 -4,-94 L -4,-84 Z", Art.S("#D93636"), Art.S("#8E1B1B"), 1);
        Art.Rc(c, -40, -90, 40, 7, Art.S("#FFFFFF"), 3);
        Art.El(c, -12, -110, 11, 11, Art.S("#FFFFFF"));
        Art.Pa(c, "M 30,-40 L 56,-26", null, Art.S("#555555"), 6);
        Art.Pa(c, "M 52,-52 L 64,-52 L 70,-4 L 58,-4 Z", Art.LG("#E6EAF0", "#9AA3B0"), Art.S("#6B7380"), 1.5);
        Art.El(c, 34, -50, 8, 8, Art.S("#FFF3A0"));
        // gundukan salju yang didorong pisau (elemen TERAKHIR; diskalakan sesuai muatan)
        Art.Pa(c, "M 66,0 C 66,-18 70,-38 88,-38 C 104,-38 112,-18 114,0 Z", Art.LG("#FFFFFF", "#C9DCF4"), Art.S("#A9C4E6"), 1.2);
    }

    // Kereta luncur Santa Claus + rusa (menghadap kanan)
    static void BuildSleigh(Canvas c)
    {
        var gold = Art.S("#F0B429");
        var brown = Art.S("#6B4122");
        Art.El(c, -92, -72, 50, 46, Art.RG("#B07A3F", "#6B4122"), Art.S("#4A2C14"), 1.5);
        Art.Pa(c, "M -70,-70 L -64,-80 L -58,-70", null, Art.S("#4A2C14"), 2);
        Art.El(c, -12, -78, 32, 36, Art.LG("#E5473F", "#A31E24"));
        Art.El(c, -6, -98, 22, 22, Art.S("#F6D3B3"));
        Art.Pa(c, "M -6,-86 Q 5,-68 16,-86 Z", Art.S("#FFFFFF"));
        Art.Pa(c, "M -7,-94 Q 6,-120 18,-94 Z", Art.S("#D93636"));
        Art.Rc(c, -8, -97, 24, 5, Art.S("#FFFFFF"), 2);
        Art.El(c, 14, -118, 8, 8, Art.S("#FFFFFF"));
        Art.Pa(c, "M -62,-14 Q -66,-46 -30,-48 L 28,-48 Q 52,-48 58,-68 Q 68,-46 52,-20 Q 0,-6 -62,-14 Z", Art.LG("#E5473F", "#A31E24"), Art.S("#7A1717"), 1.5);
        Art.Pa(c, "M -58,-24 Q 0,-14 50,-28", null, gold, 3);
        Art.Pa(c, "M -70,-4 Q 0,6 64,-12 Q 76,-16 74,-30", null, gold, 4);
        Art.Pa(c, "M 58,-40 L 90,-46", null, brown, 2);
        Art.El(c, 88, -66, 40, 24, Art.LG("#A06A38", "#7A4A25"));
        Art.El(c, 118, -86, 20, 18, Art.LG("#A06A38", "#7A4A25"));
        Art.Pa(c, "M 126,-86 L 124,-102 M 123,-96 L 117,-100 M 131,-86 L 134,-100", null, brown, 3);
        Art.El(c, 134, -78, 7, 7, Art.RG("#FF6A5A", "#C21F1F"));
        Art.Pa(c, "M 94,-46 L 90,-14 M 104,-44 L 106,-14 M 116,-44 L 120,-14 M 124,-48 L 134,-16", null, brown, 4);
    }
}
