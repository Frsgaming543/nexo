using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WPath = System.Windows.Shapes.Path;
namespace NexoIsland;

// Lampu hias yang DIKAITKAN dari satu jendela ke jendela lain (kabel menjuntai di antara dua tepi atas jendela).
//  - seret gulungan "Lampu hias" lalu lepas di tepi atas jendela pertama -> ujung kabel terpasang di situ
//  - kabel mengikuti kursor; klik di tepi atas jendela lain -> ujung kedua terkait (klik jendela yang sama = lampu di jendela itu saja)
//  - Esc / klik kanan = batal | kedua jendela digeser -> kabel ikut tertarik & berayun
//  - lepas: tahan Shift + klik kanan di kabel
public partial class SnowWindow
{
    class Link
    {
        public IntPtr HA, HB; public double OA, OB;                    // jendela & jarak kait dari tepi kiri jendela
        public Canvas Host = null!; public WPath Wire = null!; public Ellipse PinA = null!, PinB = null!;
        public List<Bulb> Bulbs = new(); public List<Point> Pts = new();
        public double Sway, SwayV, PrevMx = double.NaN;                // ayunan kabel saat jendela digeser
        public string Sig = "";
    }

    readonly List<Link> links = new();
    bool linking, linkHot, wasRDownL;
    IntPtr pendH; double pendOff; long pendStart;
    Link? pendLink;
    Border? linkHint; TextBlock? linkHintText;

    bool LightBusy => strandHot || linkHot || linking;
    bool HasLights => strands.Count > 0 || links.Count > 0 || linking;

    static readonly Brush PinFill = Solid("#E8C26A"), PinEdge = Solid("#7A5C1E"), LinkWire = Solid("#2A6B45");

    Link NewLink()
    {
        var k = new Link();
        k.Host = new Canvas { IsHitTestVisible = false };
        k.Wire = new WPath { Stroke = LinkWire, StrokeThickness = 1.9, StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false };
        k.PinA = new Ellipse { Width = 7, Height = 7, Fill = PinFill, Stroke = PinEdge, StrokeThickness = 1, IsHitTestVisible = false };
        k.PinB = new Ellipse { Width = 7, Height = 7, Fill = PinFill, Stroke = PinEdge, StrokeThickness = 1, IsHitTestVisible = false };
        k.Host.Children.Add(k.Wire); k.Host.Children.Add(k.PinA); k.Host.Children.Add(k.PinB);
        lightLayer.Children.Add(k.Host);
        return k;
    }

    // dipanggil saat gulungan lampu dilepas di tepi atas jendela
    void StartLink(Cap c, double x)
    {
        CancelLinking();
        pendH = c.H;
        pendOff = Math.Clamp(x - c.Left, 8, Math.Max(8, c.Width - 8));
        pendLink = NewLink();
        pendLink.HA = c.H;
        pendLink.Host.Visibility = Visibility.Collapsed;
        linking = true; pendStart = Environment.TickCount64;
        ApplyPrefs();
    }

    void CancelLinking()
    {
        if (pendLink != null) { lightLayer.Children.Remove(pendLink.Host); pendLink = null; }
        bool was = linking;
        linking = false;
        HideHint();
        if (was && !HasLights) ApplyPrefs();
    }

    void RemoveLink(Link k)
    {
        lightLayer.Children.Remove(k.Host);
        links.Remove(k);
        if (!HasLights) ApplyPrefs();
    }

    void ClearLinks()
    {
        foreach (var k in links.ToList()) lightLayer.Children.Remove(k.Host);
        links.Clear();
        CancelLinking();
    }

    // titik kait di tepi atas jendela (false = jendela diminimize / tertutup jendela lain)
    bool AnchorPt(IntPtr h, double off, out Point pt)
    {
        pt = default;
        if (!caps.TryGetValue(h, out var cap) || cap.Segs.Count == 0) return false;
        double x = cap.Left + Math.Clamp(off, 8, Math.Max(8, cap.Width - 8));
        if (!InSegs(cap, x)) return false;
        pt = new Point(x, cap.Top - 1);
        return true;
    }

    // jendela (paling atas dulu) yang bagian tepi atasnya sedang ditunjuk kursor
    (Cap cap, double x)? FindTop(double cx, double cy)
    {
        double ax = cx + ox, ay = cy + oy;
        foreach (var w in wins)
        {
            if (caps.TryGetValue(w.H, out var c) && c.Segs.Count > 0 && cy >= c.Top - 36 && cy <= c.Top + 70)
                foreach (var (a, b) in c.Segs)
                    if (cx >= a && cx <= b) return (c, cx);
            if (w.R.Contains(ax, ay)) return null;          // kursor ada di badan jendela lain yang lebih atas
        }
        return null;
    }

    static bool NearWire(Link k, double cx, double cy)
    {
        var pts = k.Pts;
        for (int i = 1; i < pts.Count; i++)
        {
            double x1 = pts[i - 1].X, y1 = pts[i - 1].Y, x2 = pts[i].X, y2 = pts[i].Y;
            double dx = x2 - x1, dy = y2 - y1, l2 = dx * dx + dy * dy;
            double u = l2 < 1e-6 ? 0 : Math.Clamp(((cx - x1) * dx + (cy - y1) * dy) / l2, 0, 1);
            double px = x1 + dx * u - cx, py = y1 + dy * u - cy;
            if (px * px + py * py < 144) return true;
        }
        return false;
    }

    void ShowHint(double cx, double cy, string text)
    {
        if (linkHint == null)
        {
            linkHintText = new TextBlock { FontSize = 12.5, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Segoe UI") };
            linkHint = new Border
            {
                Width = 250, Padding = new Thickness(11, 7, 11, 8), CornerRadius = new CornerRadius(10), IsHitTestVisible = false,
                Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x0B, 0x10, 0x1C)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 255, 255, 255)), BorderThickness = new Thickness(1),
                Child = linkHintText
            };
            sparkLayer.Children.Add(linkHint);
        }
        linkHintText!.Text = text;
        linkHint.Visibility = Visibility.Visible;
        Canvas.SetLeft(linkHint, Math.Clamp(cx + 22, 6, Math.Max(6, vw - 256)));
        Canvas.SetTop(linkHint, Math.Clamp(cy + 22, 6, Math.Max(6, vh - 70)));
    }

    void HideHint() { if (linkHint != null) linkHint.Visibility = Visibility.Collapsed; }

    // dipanggil setiap frame setelah UpdateCaps
    void UpdateLinks(double cx, double cy, bool click, bool rdown)
    {
        bool rclick = rdown && !wasRDownL; wasRDownL = rdown;
        linkHot = false;
        if (links.Count == 0 && !linking) { HideHint(); return; }

        var p = Prefs.Current;
        double t = clock.Elapsed.TotalSeconds;
        bool shift = (Native.GetAsyncKeyState(0x10) & 0x8000) != 0;
        bool esc = (Native.GetAsyncKeyState(0x1B) & 0x8000) != 0;
        Link? hover = null;

        foreach (var k in links.ToList())
        {
            if (!Desk.Alive(k.HA) || !Desk.Alive(k.HB)) { RemoveLink(k); continue; }     // salah satu jendela ditutup
            if (!AnchorPt(k.HA, k.OA, out var a) || !AnchorPt(k.HB, k.OB, out var b))
            {
                k.Host.Visibility = Visibility.Collapsed; k.PrevMx = double.NaN;           // diminimize / tertutup jendela lain
                continue;
            }
            k.Host.Visibility = Visibility.Visible;
            k.PinB.Visibility = Visibility.Visible;
            StepLink(k, a, b, p, t);
            if (shift && hover == null && NearWire(k, cx, cy)) hover = k;
        }

        if (linking && pendLink != null)
        {
            if (!Desk.Alive(pendH) || !AnchorPt(pendH, pendOff, out var a) || esc || rclick || Environment.TickCount64 - pendStart > 90000)   // batal otomatis setelah 90 dtk
            {
                CancelLinking();
            }
            else
            {
                var tgt = FindTop(cx, cy);
                Point b = tgt != null ? new Point(tgt.Value.x, tgt.Value.cap.Top - 1) : new Point(cx, cy);
                pendLink.Host.Visibility = Visibility.Visible;
                pendLink.PinB.Visibility = tgt != null ? Visibility.Visible : Visibility.Collapsed;
                StepLink(pendLink, a, b, p, t);
                bool same = tgt != null && tgt.Value.cap.H == pendH;
                ShowHint(cx, cy, tgt == null ? "Klik tepi atas jendela lain untuk mengaitkan lampu  ·  Esc / klik kanan = batal"
                                 : same ? "Klik = lampu di jendela ini saja" : "Klik untuk mengaitkan di sini");

                if (click && tgt != null)
                {
                    var (tc, tx) = tgt.Value;
                    var k = pendLink;
                    if (same)
                    {
                        CancelLinking();
                        AttachStrand(tc);
                    }
                    else
                    {
                        k.OA = pendOff; k.HB = tc.H; k.OB = tx - tc.Left;
                        links.Add(k);
                        pendLink = null; linking = false;
                        HideHint();
                        Spray(tx, tc.Top - 4, 14, 3.5, true, Gold);
                        ApplyPrefs();
                    }
                }
            }
        }
        else HideHint();

        linkHot = hover != null;
        if (hover != null && rclick)
        {
            Spray(cx, cy, 8, 3, true, Gold);
            RemoveLink(hover);
            linkHot = false;
        }
    }

    void StepLink(Link k, Point a, Point b, AppSettings p, double t)
    {
        // ayunan: bagian tengah kabel tertinggal saat jendela digeser, lalu berayun kembali
        double mx = (a.X + b.X) / 2;
        if (!double.IsNaN(k.PrevMx)) k.SwayV += -Math.Clamp(mx - k.PrevMx, -60, 60) * 0.45;
        k.PrevMx = mx;
        k.SwayV += -k.Sway * 0.07; k.SwayV *= 0.93; k.Sway = Math.Clamp(k.Sway + k.SwayV, -90, 90);
        if (Math.Abs(k.Sway) < 0.02 && Math.Abs(k.SwayV) < 0.02) { k.Sway = 0; k.SwayV = 0; }

        string sig = FormattableString.Invariant($"{a.X:0.#}|{a.Y:0.#}|{b.X:0.#}|{b.Y:0.#}|{k.Sway:0.#}|{p.LightSpacing:0.#}|{p.LightSag:0.#}");
        if (sig != k.Sig) { k.Sig = sig; RebuildLink(k, a, b, p); }
        if (frame % 2 == 0) foreach (var bl in k.Bulbs) LightStyle.Paint(bl, t);
    }

    void RebuildLink(Link k, Point a, Point b, AppSettings p)
    {
        double sp = Math.Clamp(p.LightSpacing, 12, 80);
        double dx = b.X - a.X, dy = b.Y - a.Y, dist = Math.Sqrt(dx * dx + dy * dy);
        double sag = Math.Min(260, Math.Clamp(p.LightSag, 0, 50) * 0.8 + dist * 0.1);          // makin jauh makin menjuntai
        var c = new Point((a.X + b.X) / 2 + k.Sway, (a.Y + b.Y) / 2 + 2 * sag);               // titik kontrol kurva (turun = 2 x kendur)

        int n = Math.Clamp((int)(dist / 5) + 8, 12, 300);
        var pts = k.Pts; pts.Clear();
        for (int i = 0; i <= n; i++)
        {
            double u = (double)i / n, v = 1 - u;
            pts.Add(new Point(v * v * a.X + 2 * v * u * c.X + u * u * b.X, v * v * a.Y + 2 * v * u * c.Y + u * u * b.Y));
        }

        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(pts[0], false, false);
            ctx.PolyLineTo(pts.Skip(1).ToList(), true, true);
        }
        g.Freeze();
        k.Wire.Data = g;

        // lampu dipasang tiap `sp` px sepanjang kabel
        var pos = new List<Point>();
        double acc = 0, next = sp * 0.5;
        for (int i = 1; i < pts.Count && pos.Count < 320; i++)
        {
            double sx = pts[i].X - pts[i - 1].X, sy = pts[i].Y - pts[i - 1].Y, seg = Math.Sqrt(sx * sx + sy * sy);
            if (seg < 1e-6) continue;
            while (acc + seg >= next && pos.Count < 320)
            {
                double f = (next - acc) / seg;
                pos.Add(new Point(pts[i - 1].X + sx * f, pts[i - 1].Y + sy * f + 3));
                next += sp;
            }
            acc += seg;
        }

        while (k.Bulbs.Count < pos.Count) k.Bulbs.Add(LightStyle.MakeBulb(k.Host, 0, 0, k.Bulbs.Count));
        while (k.Bulbs.Count > pos.Count)
        {
            var last = k.Bulbs[k.Bulbs.Count - 1];
            k.Host.Children.Remove(last.Glow); k.Host.Children.Remove(last.Core);
            k.Bulbs.RemoveAt(k.Bulbs.Count - 1);
        }
        for (int i = 0; i < pos.Count; i++) LightStyle.Place(k.Bulbs[i], pos[i].X, pos[i].Y);

        Canvas.SetLeft(k.PinA, a.X - 3.5); Canvas.SetTop(k.PinA, a.Y - 3.5);
        Canvas.SetLeft(k.PinB, b.X - 3.5); Canvas.SetTop(k.PinB, b.Y - 3.5);
    }
}
