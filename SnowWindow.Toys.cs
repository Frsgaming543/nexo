using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SnowIsland;

// Mainan: buldoser (mendorong salju), kereta Santa, manusia salju yang disusun sendiri, pohon, kado.
//  - seret mainan dengan mouse | klik buldoser = jalan/berhenti | klik kereta Santa = terbang/mendarat | klik kanan = hapus
//  - bola salju bisa ditumpuk; mata/hidung/topi/syal/tangan/kancing menempel ke bola terdekat saat dilepas
//  - mainan berdiri di atas salju di tepi jendela & taskbar; jendela digoyang -> mainan ikut melompat
public partial class SnowWindow
{
    class Toy
    {
        public TK Kind; public Canvas V = null!; public List<UIElement> Blink = new(); public MatrixTransform Mt = new();
        public double X, Y, VY, R, Scale = 1, OffX, OffY, Phase;
        public int Dir = 1; public Toy? Parent; public bool Driving, Flying, Dragging; public Cap? GroundCap; public long Stamp = -1;
        public double Load; public UIElement? Mound; public MatrixTransform? MoundMt;          // buldoser: salju yang sedang didorong
        public Canvas? Lit; public List<Bulb>? Bulbs; public bool IsLit, Used, Placed;             // pohon: lampu hias | Used = sudah dipakai (dihapus)
        public bool IsBall => ToyArt.IsBall(Kind);
        public bool IsAcc => ToyArt.IsAcc(Kind);
        public double Foot => ToyArt.Foot(Kind) * Scale;
    }

    readonly List<Toy> toys = new();
    Toy? dragToy;
    double grabDX, grabDY, dragMoved;
    bool wasRDown, solid, toyHot;

    bool ToyBusy() => toyHot || dragToy != null || rollOn || rollJustEnded || strandHot;

    // ---------- membuat bola salju (otomatis) ----------
    // Geser kursor kiri-kanan di atas tumpukan salju jendela -> bola terbentuk di kursor dan makin besar selama kursor
    // menyapu salju. Klik mouse = bola dilepas (jatuh dari kursor). Tidak perlu menu.
    bool rollOn, rollJustEnded;
    double rollAmt, rollR, rollAngle, rollCharge;
    Canvas rollC = null!;
    readonly MatrixTransform rollMt = new();

    static double RollRadius(double a) => a <= 0 ? 0 : Math.Min(52, 8 + 3.2 * Math.Sqrt(a));

    void InitRoll()
    {
        rollC = new Canvas { IsHitTestVisible = false, Visibility = Visibility.Collapsed, RenderTransform = rollMt };
        Art.El(rollC, -1, -1, 2, 2, Art.RG("#FFFFFF", "#BFD5F0"), Art.S("#A9C4E6"), 0.06);
        Art.Pa(rollC, "M -0.6,-0.25 Q 0,-0.75 0.6,-0.25", null, Art.S("#B7CDEB"), 0.07);
        Art.Pa(rollC, "M -0.55,0.4 Q 0.1,0.85 0.7,0.3", null, Art.S("#B7CDEB"), 0.07);
        Art.El(rollC, 0.15, -0.1, 0.12, 0.12, Art.S("#FFFFFF", 0.9));
        Art.El(rollC, -0.4, 0.1, 0.09, 0.09, Art.S("#FFFFFF", 0.9));
        sparkLayer.Children.Add(rollC);
    }

    // kursor menyapu kiri-kanan di atas tumpukan salju cukup jauh -> mulai menggulung bola
    void UpdateRollTrigger(double cx, double cy, AppSettings p)
    {
        if (rollOn) return;
        if (!p.AutoRoll || !p.SnowCaps || dragToy != null || toyHot || (Native.GetAsyncKeyState(0x01) & 0x8000) != 0) { rollCharge = 0; return; }
        bool onSnow = false;
        foreach (var c in caps.Values)
        {
            if (c.Still < 12) continue;
            foreach (var (a, b) in c.Segs)
            {
                if (cx < a || cx > b) continue;
                double h = c.HeightAt(cx);
                if (h < 3 || cy < c.Top - h - 16 || cy > c.Top + 3) continue;
                onSnow = true; break;
            }
            if (onSnow) break;
        }
        if (onSnow && Math.Abs(cursorVx) > 3 && Math.Abs(cursorVx) > Math.Abs(cursorVy) * 1.3) rollCharge += Math.Abs(cursorVx);
        else rollCharge *= onSnow ? 0.96 : 0.8;
        if (rollCharge > 130) { rollOn = true; rollAmt = 8; rollR = 12; rollAngle = 0; rollCharge = 0; }
    }

    void UpdateRoll(double cx, double cy, bool click)
    {
        if (!rollOn) { rollC.Visibility = Visibility.Collapsed; rollR = 0; rollAmt = 0; return; }
        if (rollAmt >= 170 || click)       // klik mouse (atau sudah sebesar-besarnya) = bola dilepas
        {
            double r = RollRadius(rollAmt);
            if (r >= 14) { AddBallToy(r, cx, cy); Spray(cx, cy, 14, 3.5, true); }
            rollOn = false; rollJustEnded = true; rollAmt = 0; rollR = 0;
            rollC.Visibility = Visibility.Collapsed;
            return;
        }
        rollR += (RollRadius(rollAmt) - rollR) * 0.25;
        rollAngle += cursorVx / Math.Max(8, rollR) * 57.3;       // bola berputar mengikuti arah geser
        rollC.Visibility = Visibility.Visible;
        rollC.Opacity = 1;
        var m = Matrix.Identity;
        m.Scale(rollR, rollR); m.Rotate(rollAngle); m.Translate(cx, cy);
        rollMt.Matrix = m;
    }

    void AddBallToy(double r, double x, double y)
    {
        var t = new Toy { Kind = TK.BallM, V = ToyArt.BallVisual(r), R = r };
        t.V.RenderTransform = t.Mt;
        t.X = x; t.Y = y + r;
        toys.Add(t); toyLayer.Children.Add(t.V);
        ApplyPrefs();
    }

    // ---------- menambah / menghapus ----------
    void HandleSpawn(string kind)
    {
        if (kind == "clear")
        {
            foreach (var t in toys) toyLayer.Children.Remove(t.V);
            toys.Clear(); dragToy = null; toyHot = false; SetSolid(false);
            ApplyPrefs();
            return;
        }
        if (kind == "unlight") { ClearStrands(); return; }
        var list = new List<TK>();
        switch (kind)
        {
            case "dozer": list.Add(TK.Dozer); break;
            case "sleigh": list.Add(TK.Sleigh); break;
            case "ball-l": list.Add(TK.BallL); break;
            case "ball-m": list.Add(TK.BallM); break;
            case "ball-s": list.Add(TK.BallS); break;
            case "eyes": list.Add(TK.Eyes); break;
            case "nose": list.Add(TK.Nose); break;
            case "hat": list.Add(TK.Hat); break;
            case "scarf": list.Add(TK.Scarf); break;
            case "arms": list.Add(TK.Arms); break;
            case "buttons": list.Add(TK.Buttons); break;
            case "tree": list.Add(TK.Tree); break;
            case "gift": list.Add(TK.Gift); break;
            case "lights": list.Add(TK.Strand); break;
            case "set":
                list.AddRange(new[] { TK.BallL, TK.BallM, TK.BallS, TK.Eyes, TK.Nose, TK.Hat, TK.Scarf, TK.Arms, TK.Buttons });
                break;
        }
        if (list.Count == 0) return;
        bool wasVisible = IsVisible;
        foreach (var k in list) AddToy(k);
        ApplyPrefs();
        if (!wasVisible)   // island harus tetap di atas overlay
            foreach (var w in Application.Current.Windows.OfType<MainWindow>()) { w.Topmost = false; w.Topmost = true; }
    }

    void AddToy(TK k)
    {
        var (c, blink) = ToyArt.Build(k);
        var t = new Toy { Kind = k, V = c, Blink = blink, R = ToyArt.Radius(k) };
        c.RenderTransform = t.Mt;
        if (k == TK.Dozer) { t.Mound = c.Children[c.Children.Count - 1]; t.MoundMt = new MatrixTransform(); t.Mound.RenderTransform = t.MoundMt; t.Mound.Visibility = Visibility.Collapsed; }
        if (k == TK.Tree) { t.Lit = (Canvas)c.Children[c.Children.Count - 1]; t.Bulbs = (List<Bulb>)c.Tag; }
        t.X = vw * (0.2 + rnd.NextDouble() * 0.6);
        t.Y = -140 - rnd.NextDouble() * 120;      // jatuh dari atas layar
        toys.Add(t); toyLayer.Children.Add(c);
    }

    void RemoveToy(Toy t)
    {
        var all = new List<Toy> { t };
        for (int i = 0; i < all.Count; i++)
            foreach (var c in toys) if (c.Parent == all[i] && !all.Contains(c)) all.Add(c);
        foreach (var x in all) { toyLayer.Children.Remove(x.V); toys.Remove(x); if (dragToy == x) dragToy = null; }
        if (toys.Count == 0) ApplyPrefs();
    }

    // bawa mainan (dan semua yang menempel padanya) ke lapisan paling depan
    void Front(Toy t)
    {
        var all = new List<Toy> { t };
        for (int i = 0; i < all.Count; i++)
            foreach (var c in toys) if (c.Parent == all[i] && !all.Contains(c)) all.Add(c);
        foreach (var x in all)
        {
            toys.Remove(x); toys.Add(x);
            toyLayer.Children.Remove(x.V); toyLayer.Children.Add(x.V);
        }
    }

    // ---------- input ----------
    void SetSolid(bool on)
    {
        if (hwnd == IntPtr.Zero || on == solid) return;
        solid = on;
        Desk.SetClickThrough(hwnd, !on);     // saat kursor di atas mainan, jendela menerima klik (tidak tembus ke aplikasi lain)
        Cursor = on ? Cursors.Hand : Cursors.Arrow;
    }

    Rect ToyRect(Toy t)
    {
        var b = ToyArt.Bounds(t.Kind);
        double x0 = b.X * t.Scale, x1 = (b.X + b.Width) * t.Scale, y0 = b.Y * t.Scale, y1 = (b.Y + b.Height) * t.Scale;
        if (t.Dir < 0) { double tmp = -x1; x1 = -x0; x0 = tmp; }
        return new Rect(t.X + x0, t.Y + y0, x1 - x0, y1 - y0);
    }

    bool HitToy(Toy t, double cx, double cy)
    {
        if (t.IsBall)
        {
            double dx = cx - t.X, dy = cy - (t.Y - t.R);
            return dx * dx + dy * dy <= (t.R + 3) * (t.R + 3);
        }
        return ToyRect(t).Contains(cx, cy);
    }

    void UpdateToys(double cx, double cy, bool down, bool click, bool rdown)
    {
        bool rclick = rdown && !wasRDown; wasRDown = rdown;
        if (toys.Count == 0) { toyHot = false; SetSolid(rollOn || strandHot); return; }

        Toy? hit = null;
        if (dragToy == null)
            for (int i = toys.Count - 1; i >= 0; i--)
                if (HitToy(toys[i], cx, cy)) { hit = toys[i]; break; }
        if (rollOn || rollJustEnded) hit = null;        // saat menggulung bola, klik = melepas bola (bukan mengambil mainan)
        toyHot = hit != null;
        SetSolid(hit != null || dragToy != null || rollOn || strandHot);

        if (rclick && hit != null)
        {
            RemoveToy(hit); hit = null; toyHot = false;
        }
        else if (click && hit != null)
        {
            dragToy = hit; grabDX = cx - hit.X; grabDY = cy - hit.Y; dragMoved = 0;
            hit.Parent = null;
            if (hit.IsAcc) hit.Scale = 1;
            hit.Flying = false;
            Front(hit);
        }

        if (dragToy != null)
        {
            var t = dragToy;
            if (down)
            {
                double nx = cx - grabDX, ny = cy - grabDY;
                dragMoved += Math.Abs(nx - t.X) + Math.Abs(ny - t.Y);
                t.X = nx; t.Y = ny; t.VY = 0; t.Dragging = true;
            }
            else
            {
                dragToy = null; t.Dragging = false;
                if (dragMoved < 6) Clicked(t); else TrySnap(t);
            }
        }

        for (int i = 0; i < toys.Count; i++) Step(toys[i]);
        for (int i = 0; i < toys.Count; i++) Render(toys[i]);
        foreach (var u in toys.Where(q => q.Used).ToList()) RemoveToy(u);
    }

    void Clicked(Toy t)
    {
        switch (t.Kind)
        {
            case TK.Dozer: t.Driving = !t.Driving; break;
            case TK.Sleigh:
                t.Flying = !t.Flying; t.VY = 0;
                if (t.Flying) { t.Dir = rnd.Next(2) == 0 ? 1 : -1; t.Y -= 60; t.Phase = 0; }
                break;
            default: Spray(t.X, t.Y - 20, 8, 3, true, Gold); break;
        }
    }

    // ---------- menyusun manusia salju ----------
    bool IsDesc(Toy low, Toy t)
    {
        for (var p = low.Parent; p != null; p = p.Parent) if (p == t) return true;
        return false;
    }

    bool TrySnapBall(Toy t, bool forced)
    {
        foreach (var low in toys)
        {
            if (low == t || !low.IsBall || IsDesc(low, t)) continue;
            if (t.R > low.R + 4) continue;                                   // bola besar di bawah, kecil di atas
            if (toys.Any(q => q.Parent == low && q.IsBall)) continue;         // sudah ada bola di atasnya
            double lowTop = low.Y - 2 * low.R;
            if (Math.Abs(t.X - low.X) > 0.8 * low.R + 10) continue;
            double lo = lowTop - (forced ? 45 : 12), hi = lowTop + (forced ? t.R : 0.6 * t.R);
            if (t.Y < lo || t.Y > hi) continue;
            t.Parent = low; t.OffX = 0; t.OffY = -2 * low.R + 0.3 * t.R; t.VY = 0; t.GroundCap = null;
            Front(t);
            return true;
        }
        return false;
    }

    void TrySnap(Toy t)
    {
        if (t.IsBall) { TrySnapBall(t, true); return; }
        if (t.Kind == TK.Strand) { t.Placed = true; TrySnapStrand(t); return; }
        if (!t.IsAcc) return;
        Toy? best = null; double bd = double.MaxValue;
        foreach (var b in toys)
        {
            if (!b.IsBall) continue;
            double dx = t.X - b.X, dy = t.Y - (b.Y - b.R), d = Math.Sqrt(dx * dx + dy * dy);
            if (d < b.R + 50 && d < bd) { bd = d; best = b; }
        }
        if (best == null) return;
        foreach (var o in toys.Where(q => q.Parent == best && q.Kind == t.Kind && q != t).ToList()) { o.Parent = null; o.Scale = 1; }
        double s = best.R / 32.0;
        var sp = ToyArt.Snap(t.Kind);
        t.Parent = best; t.Scale = s; t.OffX = sp.X * s; t.OffY = sp.Y * s; t.VY = 0;
        Front(t);
        Spray(t.X, t.Y, 6, 2.5, false);
    }

    // ---------- fisika ----------
    (double y, Cap? cap) GroundAt(double x, double feet)
    {
        double best = vh; Cap? bc = null;
        foreach (var c in caps.Values)
            foreach (var (a, b) in c.Segs)
            {
                if (x < a || x > b) continue;
                double sy = c.Top - (Prefs.Current.SnowCaps ? c.HeightAt(x) * 0.7 : 0);      // berdiri di atas salju (atau tepi jendela)
                if (sy >= feet - 40 && sy < best) { best = sy; bc = c; }
            }
        return (best, bc);
    }

    void Step(Toy t)
    {
        if (t.Stamp == frame) return;
        t.Stamp = frame;

        if (t.Parent != null)
        {
            Step(t.Parent);
            var P = t.Parent;
            t.X = P.X + t.OffX;
            t.Y = t.IsBall ? P.Y + t.OffY : (P.Y - P.R) + t.OffY;
            return;
        }
        if (t.Dragging) return;
        if (t.Flying) { Fly(t); return; }

        double foot = t.Foot;
        if (t.Kind == TK.Dozer && t.Driving) Drive(t);
        var g = GroundAt(t.X, t.Y + foot);
        t.VY = Math.Min(18, t.VY + 0.6);
        double nf = t.Y + foot + t.VY;
        if (nf >= g.y)
        {
            nf = g.y;
            t.VY = t.VY > 7 ? -t.VY * 0.25 : 0;
            t.GroundCap = g.cap;
        }
        else t.GroundCap = null;
        t.Y = nf - foot;

        if (t.IsBall && t.VY > 0) TrySnapBall(t, false);
        if (t.Kind == TK.Strand && t.Placed && t.GroundCap != null && !t.Used) TrySnapStrand(t);     // gulungan lampu yang mendarat di jendela langsung terpasang
        if (t.Kind == TK.Dozer && t.Driving) Blade(t);
    }

    void Drive(Toy t)
    {
        t.X += t.Dir * Math.Max(0.9, 1.7 - t.Load * 0.014);        // makin berat muatan salju, makin pelan
        var ahead = GroundAt(t.X + t.Dir * 75, t.Y);
        if (ahead.y > t.Y + 28 || t.X < 30 || t.X > vw - 30) { Dump(t); t.Dir = -t.Dir; }     // di tepi jendela / layar: salju terdorong jatuh, lalu putar balik
    }

    // pisau buldoser MENDORONG salju: salju di depan pisau terkumpul jadi gundukan yang ikut berjalan,
    // dan baru jatuh saat buldoser sampai di tepi jendela (atau kalau gundukan terlalu besar)
    void Blade(Toy t)
    {
        double bx = t.X + t.Dir * 70;
        foreach (var c in caps.Values)
        {
            if (!InSegs(c, bx) || Math.Abs(c.Top - t.Y) > 45) continue;
            double rem = c.Plow(bx, t.Dir, 12);
            if (rem <= 0) continue;
            t.Load += rem * 0.12;
            if (rnd.NextDouble() < 0.3) Spray(bx, c.Top - 6, 1, 1.4, false);
            if (t.Load > 40)
            {
                double over = t.Load - 40; t.Load = 40;
                SpawnChunks(bx + t.Dir * 34, c.Top - 26, Math.Min(4, (int)(over * 0.8) + 1), t.Dir * 1.6, 1.2);
            }
        }
    }

    void Dump(Toy t)
    {
        if (t.Load < 1) { t.Load = 0; return; }
        int n = Math.Min(40, (int)(t.Load * 1.2) + 3);
        double bx = t.X + t.Dir * 74;
        SpawnChunks(bx, t.Y - 30, n, t.Dir * 2.4, 2.0, null, -1.5);
        Spray(bx, t.Y - 30, 8, 3, true);
        t.Load = 0;
    }

    // jendela bergerak -> mainan yang berdiri di atasnya ikut terbawa
    void CarryToys(Cap c, double vx, double vy)
    {
        if (Math.Abs(vx) > 400 || Math.Abs(vy) > 400) return;
        foreach (var t in toys)
            if (t.Parent == null && !t.Dragging && !t.Flying && t.GroundCap == c) { t.X += vx; t.Y += vy; }
    }

    // gulungan lampu hias: dilepas di dekat pohon -> pohon menyala; dilepas di tepi atas jendela -> kabel lampu terpasang di jendela itu
    void TrySnapStrand(Toy t)
    {
        foreach (var tr in toys)
        {
            if (tr.Kind != TK.Tree || tr.IsLit) continue;
            var r = ToyRect(tr); r.Inflate(40, 30);
            if (!r.Contains(t.X, t.Y - 10)) continue;
            tr.IsLit = true; tr.Lit!.Visibility = Visibility.Visible;
            Spray(tr.X, tr.Y - 120, 16, 4, true, Gold);
            t.Used = true; return;
        }
        foreach (var c in caps.Values)
            foreach (var (a, b) in c.Segs)
            {
                if (t.X < a || t.X > b || t.Y < c.Top - 110 || t.Y > c.Top + 40) continue;
                AttachStrand(c);
                Spray(t.X, c.Top - 4, 14, 3.5, true, Gold);
                t.Used = true; return;
            }
    }

    // kereta Santa terbang melintasi layar, meninggalkan kilau; anginnya menerbangkan salju di bawahnya
    void Fly(Toy t)
    {
        t.Phase += 0.05;
        t.X += t.Dir * 3.4;
        t.Y += Math.Sin(t.Phase) * 0.8;
        if (t.X > vw + 150) t.X = -150; else if (t.X < -150) t.X = vw + 150;
        if (frame % 2 == 0) Spray(t.X - t.Dir * 85, t.Y - 30, 1, 1.4, false, Gold);
        foreach (var c in caps.Values)
            if (InSegs(c, t.X) && c.Top > t.Y && c.Top - t.Y < 90)
            {
                double rem = c.Wipe(t.X, 50, 0.7);
                if (rem > 0.3) SpawnChunks(t.X, c.Top - c.HeightAt(t.X), 2, t.Dir * 2, 1.5);
            }
    }

    // jendela digoyang -> mainan yang berdiri di atasnya melompat
    void ShakeToys(Cap c)
    {
        foreach (var t in toys)
            if (t.Parent == null && !t.Dragging && !t.Flying && t.GroundCap == c) t.VY = -(7 + rnd.NextDouble() * 3);
    }

    void Render(Toy t)
    {
        var m = Matrix.Identity;
        m.Scale(t.Scale * t.Dir, t.Scale);
        m.Translate(t.X, t.Y);
        t.Mt.Matrix = m;
        for (int j = 0; j < t.Blink.Count; j++) t.Blink[j].Opacity = ((frame / 20 + j) & 1) == 0 ? 1 : 0.35;
        if (t.Mound != null)
        {
            bool on = t.Load > 0.6;
            t.Mound.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            if (on) { var mm = Matrix.Identity; mm.ScaleAt(0.3 + t.Load * 0.035, 0.25 + t.Load * 0.04, 66, 0); t.MoundMt!.Matrix = mm; }
        }
        if (t.IsLit && t.Bulbs != null && frame % 2 == 0)
        {
            double tm = clock.Elapsed.TotalSeconds;
            foreach (var b in t.Bulbs) LightStyle.Paint(b, tm);
        }
    }
}
