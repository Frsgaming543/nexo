using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Ellipse = System.Windows.Shapes.Ellipse;
using Polygon = System.Windows.Shapes.Polygon;
using Polyline = System.Windows.Shapes.Polyline;
using WPath = System.Windows.Shapes.Path;

namespace SnowIsland;

// Halaman-halaman island: dock navigasi, To-do, Fokus, Kalender, Cuaca, Sistem
public partial class MainWindow
{
    // ===================== Util =====================
    // Busur lingkaran mulai dari atas, searah jarum jam (frac 0..1). Null kalau terlalu kecil.
    static Geometry? Arc(double cx, double cy, double rad, double frac)
    {
        frac = Math.Clamp(frac, 0, 0.9999);
        if (frac < 0.004) return null;
        double a = 2 * Math.PI * frac;
        var end = new Point(cx + rad * Math.Sin(a), cy - rad * Math.Cos(a));
        var fig = new PathFigure { StartPoint = new Point(cx, cy - rad), IsClosed = false };
        fig.Segments.Add(new ArcSegment(end, new Size(rad, rad), 0, frac > 0.5, SweepDirection.Clockwise, true));
        var g = new PathGeometry();
        g.Figures.Add(fig);
        return g;
    }

    static readonly string[] MonthNames = { "Januari", "Februari", "Maret", "April", "Mei", "Juni", "Juli", "Agustus", "September", "Oktober", "November", "Desember" };
    static readonly string[] DayNames = { "Minggu", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu" };
    static readonly string[] DayShort3 = { "Min", "Sen", "Sel", "Rab", "Kam", "Jum", "Sab" };
    static string DKey(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    static DateTime ParseKey(string k) => DateTime.ParseExact(k, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    static string ShortDate(string k) { var d = ParseKey(k); return d.Day + " " + MonthNames[d.Month - 1].Substring(0, 3); }

    static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SnowIsland");

    // ===================== Dock navigasi =====================
    // urutan tampil di dock (nomor halaman: 0 musik, 1 shalat, 2 fokus, 3 to-do, 4 kalender, 5 cuaca, 6 sistem)
    static readonly int[] Order = { 0, 4, 3, 2, 1, 5, 6 };
    static readonly string[] PgIcons = { "🎵", "🕌", "⏱", "✅", "🗓", "⛅", "📊" };
    static readonly string[] PgNames = { "Musik", "Shalat", "Fokus", "Tugas", "Kalender", "Cuaca", "Sistem" };
    static readonly Brush DockOn = new SolidColorBrush(Color.FromArgb(0x38, 255, 255, 255));
    readonly Border[] dock = new Border[7];
    readonly TextBlock[] dockLbl = new TextBlock[7];

    static int Pos(int p) => Array.IndexOf(Order, p);

    void BuildDock()
    {
        foreach (int p0 in Order)
        {
            int p = p0;
            var icon = new TextBlock { Text = PgIcons[p], FontFamily = new FontFamily("Segoe UI Emoji"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            var lbl = new TextBlock
            {
                Text = PgNames[p], FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
                Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(icon); sp.Children.Add(lbl);
            var b = new Border
            {
                Height = 26, MinWidth = 32, CornerRadius = new CornerRadius(13), Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(2, 0, 2, 0), Background = Brushes.Transparent, Cursor = Cursors.Hand,
                ToolTip = PgNames[p], Child = sp, Opacity = 0.55
            };
            b.MouseLeftButtonUp += (_, _) => GoPage(p);
            dock[p] = b; dockLbl[p] = lbl;
            Tabs.Children.Add(b);
        }
    }

    void GoPage(int p)
    {
        if (p == page) return;
        SetPage(p, Pos(p) > Pos(page) ? 1 : -1);
    }

    void SetPage(int p, int dir = 0)
    {
        int old = page;
        page = p;
        var pages = new FrameworkElement[] { MusicPage, PrayerPage, FocusPage, TodoPage, CalendarPage, WeatherPage, SystemPage };
        for (int i = 0; i < pages.Length; i++) pages[i].Visibility = i == p ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = PgNames[p];
        for (int i = 0; i < dock.Length; i++)
        {
            bool on = i == p;
            dock[i].Background = on ? DockOn : Brushes.Transparent;
            dock[i].Opacity = on ? 1 : 0.55;
            dockLbl[i].Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        }
        if (p != 0 && lyricsMode) { lyricsMode = false; LyricsCol.Visibility = Visibility.Collapsed; }
        SetSize();
        if (p == 6) netInit = false;
        if (p == 5 && (DateTime.Now - wxAt).TotalMinutes > 10) _ = LoadWeather();
        if (dir != 0 && p != old) SlideIn(pages[p], dir);
    }

    // ===================== To-do =====================
    int newPri;
    Todo? justAdded, popped;
    static readonly string[] PriHex = { "#66FFFFFF", "#FFFFC857", "#FFFF6B7A" };
    static readonly string[] PriLabel = { "Prioritas biasa", "Prioritas penting", "Prioritas mendesak" };

    void BuildTodoUi()
    {
        TodoArc.Stroke = accentBr;
        UpdatePriDot();
    }

    void UpdatePriDot()
    {
        var c = Solid(PriHex[newPri]);
        TodoPriDot.Fill = newPri == 0 ? Brushes.Transparent : c;
        TodoPriDot.Stroke = newPri == 0 ? Solid("#99FFFFFF") : c;
        TodoPriDot.StrokeThickness = 1.6;
        TodoPriBtn.ToolTip = PriLabel[newPri] + " (klik untuk ganti)";
    }

    void LoadTodos()
    {
        try
        {
            var f = Path.Combine(DataDir, "todos.json");
            if (File.Exists(f)) todos = JsonSerializer.Deserialize<List<Todo>>(File.ReadAllText(f)) ?? new();
        }
        catch { }
        RenderTodos();
    }

    void SaveTodos()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(Path.Combine(DataDir, "todos.json"), JsonSerializer.Serialize(todos));
        }
        catch { }
    }

    void RenderTodos()
    {
        TodoList.Children.Clear();
        int total = todos.Count, done = todos.Count(x => x.Done);
        TodoArc.Data = Arc(50, 50, 44, total == 0 ? 0 : (double)done / total);
        TodoPct.Text = total == 0 ? "–" : (int)Math.Round(100.0 * done / total) + "%";
        TodoCount.Text = total == 0 ? "Belum ada tugas" : done == total ? "Semua selesai 🎉" : $"{done} dari {total} selesai";
        TodoClear.Visibility = done > 0 ? Visibility.Visible : Visibility.Collapsed;
        TodoEmpty.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var item in todos.OrderBy(x => x.Done).ThenByDescending(x => x.Pri)) TodoList.Children.Add(MakeTodoRow(item));
        popped = null;
        UpdateFocusUI();
    }

    UIElement MakeTodoRow(Todo t)
    {
        var rest = new SolidColorBrush(Color.FromArgb(0x14, 255, 255, 255));
        var hover = new SolidColorBrush(Color.FromArgb(0x26, 255, 255, 255));
        var row = new Border
        {
            Height = 34, CornerRadius = new CornerRadius(11), Margin = new Thickness(0, 0, 0, 5), Background = rest,
            BorderBrush = CardEdge, BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
            RenderTransform = new TranslateTransform()
        };
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

        // penanda prioritas (garis kecil di kiri)
        var bar = new Border
        {
            Width = 3, Height = 16, CornerRadius = new CornerRadius(1.5), HorizontalAlignment = HorizontalAlignment.Center,
            Background = t.Pri == 0 ? Brushes.Transparent : Solid(PriHex[t.Pri])
        };

        // lingkaran centang
        var chk = new Grid { Width = 20, Height = 20, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1) };
        chk.Children.Add(new Ellipse { Stroke = Solid("#88FFFFFF"), StrokeThickness = 1.6, Visibility = t.Done ? Visibility.Collapsed : Visibility.Visible });
        chk.Children.Add(new Ellipse { Fill = accentBr, Visibility = t.Done ? Visibility.Visible : Visibility.Collapsed });
        chk.Children.Add(new WPath
        {
            Data = Geometry.Parse("M5.5,10.3 L8.8,13.4 L14.6,6.8"), Stroke = Solid("#FF0A1226"), StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            Visibility = t.Done ? Visibility.Visible : Visibility.Collapsed
        });
        if (ReferenceEquals(t, popped))
        {
            var st = (ScaleTransform)chk.RenderTransform;
            var an = new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = new BackEase { Amplitude = 0.6, EasingMode = EasingMode.EaseOut } };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, an);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, an);
        }

        var txt = new TextBlock
        {
            Text = t.Text, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = t.Done ? Dim : Brushes.White, TextDecorations = t.Done ? TextDecorations.Strikethrough : null,
            Margin = new Thickness(2, 0, 6, 0)
        };

        // titik prioritas (klik = ganti)
        var pdot = new Grid { Background = Brushes.Transparent, ToolTip = PriLabel[t.Pri] };
        pdot.Children.Add(new Ellipse
        {
            Width = 9, Height = 9, Fill = t.Pri == 0 ? Brushes.Transparent : Solid(PriHex[t.Pri]),
            Stroke = t.Pri == 0 ? Solid("#55FFFFFF") : Solid(PriHex[t.Pri]), StrokeThickness = 1.3
        });
        pdot.MouseLeftButtonUp += (_, e) => { e.Handled = true; t.Pri = (t.Pri + 1) % 3; SaveTodos(); RenderTodos(); };

        // hapus (muncul saat hover)
        var del = new TextBlock { Text = "✕", FontSize = 11, Foreground = Brushes.White, Opacity = 0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var delHit = new Grid { Background = Brushes.Transparent, ToolTip = "Hapus" };
        delHit.Children.Add(del);
        delHit.MouseLeftButtonUp += (_, e) => { e.Handled = true; todos.Remove(t); SaveTodos(); RenderTodos(); };

        Grid.SetColumn(bar, 0); Grid.SetColumn(chk, 1); Grid.SetColumn(txt, 2); Grid.SetColumn(pdot, 3); Grid.SetColumn(delHit, 4);
        g.Children.Add(bar); g.Children.Add(chk); g.Children.Add(txt); g.Children.Add(pdot); g.Children.Add(delHit);
        row.Child = g;

        row.MouseEnter += (_, _) => { row.Background = hover; del.Opacity = 0.75; };
        row.MouseLeave += (_, _) => { row.Background = rest; del.Opacity = 0; };
        row.MouseLeftButtonUp += (_, _) => { t.Done = !t.Done; popped = t; SaveTodos(); RenderTodos(); };

        if (ReferenceEquals(t, justAdded))
        {
            var tt = (TranslateTransform)row.RenderTransform;
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-12, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            row.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)));
        }
        return row;
    }

    void AddTodo()
    {
        var text = TodoBox.Text.Trim();
        if (text == "") return;
        var t = new Todo { Text = text, Pri = newPri };
        todos.Insert(0, t);
        justAdded = t;
        TodoBox.Clear();
        newPri = 0; UpdatePriDot();
        SaveTodos(); RenderTodos();
        justAdded = null;
    }

    void Todo_Pri(object s, RoutedEventArgs e) { newPri = (newPri + 1) % 3; UpdatePriDot(); }
    void Todo_Add(object s, RoutedEventArgs e) => AddTodo();
    void Todo_Clear(object s, RoutedEventArgs e) { todos.RemoveAll(t => t.Done); SaveTodos(); RenderTodos(); }
    void Todo_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddTodo();
        e.Handled = true;
    }
    void Todo_TextChanged(object s, TextChangedEventArgs e) =>
        TodoHint.Visibility = TodoBox.Text == "" ? Visibility.Visible : Visibility.Collapsed;

    // ===================== Fokus (Pomodoro) =====================
    class FocusStat { public string Date { get; set; } = ""; public int Sessions { get; set; } public int Minutes { get; set; } }
    FocusStat fstat = new();
    Todo? focusTask;
    readonly Button[] presetBtns = new Button[3];

    static void ChipOn(Button b, bool on, bool seg = false)
    {
        b.Background = on ? Brushes.White : seg ? Brushes.Transparent : Solid("#1FFFFFFF");
        b.Foreground = on ? Solid("#FF0A1226") : Brushes.White;
    }

    void BuildFocusUi()
    {
        FocusMiniArc.Stroke = accentBr;
        for (int i = 0; i < 4; i++)
        {
            fDots[i] = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(i == 0 ? 0 : 5, 0, 0, 0), Stroke = accentBr, StrokeThickness = 1.4, Fill = Brushes.Transparent };
            FocusDots.Children.Add(fDots[i]);
        }
        for (int i = 0; i < presets.Length; i++)
        {
            int idx = i;
            var b = new Button
            {
                Style = (Style)FindResource("Chip"), Content = $"{presets[i][0]} · {presets[i][1]}", Height = 24, FontSize = 11.5,
                Padding = new Thickness(11, 0, 11, 0), Margin = new Thickness(i == 0 ? 0 : 5, 0, 0, 0), ToolTip = $"{presets[i][0]} menit fokus, {presets[i][1]} menit istirahat"
            };
            b.Click += (_, _) => { presetIdx = idx; focusRunning = false; FocusStart(false); UpdateFocusUI(); };
            presetBtns[i] = b;
            FocusPresets.Children.Add(b);
        }
        try
        {
            var f = Path.Combine(DataDir, "focus.json");
            if (File.Exists(f)) fstat = JsonSerializer.Deserialize<FocusStat>(File.ReadAllText(f)) ?? new();
        }
        catch { }
        if (fstat.Date != DKey(DateTime.Today)) fstat = new FocusStat { Date = DKey(DateTime.Today) };
    }

    void AddFocusStat(int minutes)
    {
        if (fstat.Date != DKey(DateTime.Today)) fstat = new FocusStat { Date = DKey(DateTime.Today) };
        fstat.Sessions++; fstat.Minutes += minutes;
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(Path.Combine(DataDir, "focus.json"), JsonSerializer.Serialize(fstat));
        }
        catch { }
    }

    void FocusStart(bool brk)
    {
        focusBreak = brk;
        int m = presets[presetIdx][brk ? 1 : 0];
        if (brk && focusDone > 0 && focusDone % 4 == 0) m *= 3;        // istirahat panjang tiap 4 sesi
        focusTotal = focusLeft = m * 60;
    }

    void FocusFinish()
    {
        if (!focusBreak)
        {
            AddFocusStat(Math.Max(1, focusTotal / 60));
            focusDone++; FocusStart(true); focusRunning = true;
            Notify("🍅", "Sesi fokus selesai", focusTask != null ? focusTask.Text : "Waktunya istirahat sebentar", "#E5484D", 6, "focus");
        }
        else
        {
            FocusStart(false); focusRunning = false;
            Notify("☕", "Istirahat selesai", focusTask != null ? "Lanjut: " + focusTask.Text : "Siap fokus lagi?", "#2BB673", 6, "focus");
        }
    }

    void UpdateFocusUI()
    {
        if (focusTask != null && (focusTask.Done || !todos.Contains(focusTask))) focusTask = null;

        string mmss = TimeSpan.FromSeconds(Math.Max(0, focusLeft)).ToString(@"mm\:ss");
        FocusTime.Text = mmss; FocusText.Text = mmss;
        FocusLabel.Text = focusBreak ? "ISTIRAHAT" : "FOKUS";
        FocusMini.Text = focusTask != null ? focusTask.Text : focusBreak ? "Istirahat" : "Fokus";
        FocusBtn.Content = focusRunning ? "⏸  Jeda" : focusLeft < focusTotal ? "▶  Lanjut" : "▶  Mulai";
        ChipOn(FocusSegWork, !focusBreak, true); ChipOn(FocusSegBreak, focusBreak, true);
        for (int i = 0; i < presetBtns.Length; i++) if (presetBtns[i] != null) ChipOn(presetBtns[i], i == presetIdx);

        FocusTaskText.Text = focusTask != null ? focusTask.Text : "Pilih tugas untuk difokuskan";
        FocusTaskText.Opacity = focusTask != null ? 1 : 0.55;
        FocusTaskDone.Visibility = focusTask != null ? Visibility.Visible : Visibility.Collapsed;
        FocusStats.Text = fstat.Sessions == 0 ? "Belum ada sesi hari ini" : $"Hari ini · {fstat.Sessions} sesi · {fstat.Minutes} mnt";

        double frac = Math.Clamp(1 - (double)focusLeft / Math.Max(1, focusTotal), 0, 0.9999);
        FocusArc.Data = Arc(66, 66, 58, frac);
        FocusMiniArc.Data = Arc(13, 13, 10, frac);
        double ang = 2 * Math.PI * frac;
        Canvas.SetLeft(FocusKnob, 66 + 58 * Math.Sin(ang) - 5.5);
        Canvas.SetTop(FocusKnob, 66 - 58 * Math.Cos(ang) - 5.5);

        int filled = focusBreak && focusDone % 4 == 0 && focusDone > 0 ? 4 : focusDone % 4;
        for (int i = 0; i < 4; i++) if (fDots[i] != null) fDots[i].Fill = i < filled ? accentBr : Brushes.Transparent;
    }

    void Focus_Toggle(object s, RoutedEventArgs e) { focusRunning = !focusRunning; UpdateFocusUI(); }
    void Focus_Reset(object s, RoutedEventArgs e) { focusRunning = false; FocusStart(focusBreak); UpdateFocusUI(); }
    void Focus_Skip(object s, RoutedEventArgs e) { FocusStart(!focusBreak); UpdateFocusUI(); }
    void Focus_SegWork(object s, RoutedEventArgs e) { focusRunning = false; FocusStart(false); UpdateFocusUI(); }
    void Focus_SegBreak(object s, RoutedEventArgs e) { focusRunning = false; FocusStart(true); UpdateFocusUI(); }

    void Focus_PickTask(object s, RoutedEventArgs e)
    {
        var open = todos.Where(t => !t.Done).OrderByDescending(t => t.Pri).ToList();
        if (open.Count == 0) focusTask = null;
        else
        {
            int i = focusTask == null ? -1 : open.IndexOf(focusTask);
            focusTask = i + 1 < open.Count ? open[i + 1] : null;   // putar: tugas 1, 2, ... lalu tanpa tugas
        }
        UpdateFocusUI();
    }

    void Focus_TaskDone(object s, RoutedEventArgs e)
    {
        if (focusTask == null) return;
        focusTask.Done = true; popped = focusTask; focusTask = null;
        SaveTodos(); RenderTodos();
    }

    // ===================== Kalender =====================
    class CalEvent
    {
        public string Date { get; set; } = "";
        public string Title { get; set; } = "";
        public string Time { get; set; } = "";
        public int Color { get; set; }
    }
    List<CalEvent> events = new();
    DateTime calMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    DateTime calSel = DateTime.Today;
    int newEvColor;
    string calToday = "";
    readonly HashSet<string> remNotified = new();
    static readonly string[] EvHex = { "#FF5AA9FF", "#FFFF7AB6", "#FF4ADE80", "#FFFFC857", "#FFA78BFA" };

    void BuildCalendarUi()
    {
        string[] wk = { "Sen", "Sel", "Rab", "Kam", "Jum", "Sab", "Min" };
        foreach (var name in wk)
            CalWeek.Children.Add(new TextBlock
            {
                Text = name, FontSize = 9.5, FontWeight = FontWeights.SemiBold, Opacity = 0.85,
                Foreground = name == "Min" ? Solid("#FFFF9AA2") : Dim,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            });
        CalColorDot.Fill = Solid(EvHex[0]);
        RenderCalendar(); RenderDay();
    }

    void LoadEvents()
    {
        try
        {
            var f = Path.Combine(DataDir, "events.json");
            if (File.Exists(f)) events = JsonSerializer.Deserialize<List<CalEvent>>(File.ReadAllText(f)) ?? new();
        }
        catch { }
        RenderCalendar(); RenderDay();
    }

    void SaveEvents()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(Path.Combine(DataDir, "events.json"), JsonSerializer.Serialize(events));
        }
        catch { }
    }

    void RenderCalendar()
    {
        CalTitle.Text = $"{MonthNames[calMonth.Month - 1]} {calMonth.Year}";
        CalGrid.Children.Clear();
        int offset = ((int)calMonth.DayOfWeek + 6) % 7;                 // minggu dimulai Senin
        var start = calMonth.AddDays(-offset);
        var byDay = events.GroupBy(e => e.Date).ToDictionary(g => g.Key, g => g.Select(e => e.Color).Distinct().Take(3).ToList());
        string todayKey = DKey(DateTime.Today);

        for (int i = 0; i < 42; i++)
        {
            var d = start.AddDays(i);
            string k = DKey(d);
            bool isToday = k == todayKey, isSel = d.Date == calSel.Date, other = d.Month != calMonth.Month;
            var cell = new Border
            {
                Margin = new Thickness(1.5, 1, 1.5, 1), CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand,
                Background = isToday ? accentBr : isSel ? Solid("#26FFFFFF") : Brushes.Transparent,
                BorderBrush = isSel ? Solid("#CCFFFFFF") : Brushes.Transparent, BorderThickness = new Thickness(1)
            };
            var g = new Grid();
            g.Children.Add(new TextBlock
            {
                Text = d.Day.ToString(), FontSize = 11, FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -2, 0, 0),
                Foreground = isToday ? Solid("#FF0A1226") : d.DayOfWeek == DayOfWeek.Sunday ? Solid("#FFFF9AA2") : Brushes.White,
                Opacity = other ? 0.3 : 1
            });
            if (byDay.TryGetValue(k, out var cols))
            {
                var dots = new StackPanel
                {
                    Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 1.5), Opacity = other ? 0.35 : 1
                };
                foreach (var c in cols)
                    dots.Children.Add(new Ellipse
                    {
                        Width = 3.5, Height = 3.5, Margin = new Thickness(0.8, 0, 0.8, 0),
                        Fill = isToday ? Solid("#FF0A1226") : Solid(EvHex[c % EvHex.Length])
                    });
                g.Children.Add(dots);
            }
            cell.Child = g;
            var dd = d;
            cell.MouseLeftButtonUp += (_, _) =>
            {
                calSel = dd.Date;
                if (dd.Month != calMonth.Month || dd.Year != calMonth.Year) calMonth = new DateTime(dd.Year, dd.Month, 1);
                RenderCalendar(); RenderDay();
                CalBox.Focus();
            };
            CalGrid.Children.Add(cell);
        }
    }

    void RenderDay()
    {
        int rel = (calSel.Date - DateTime.Today).Days;
        string relText = rel == 0 ? "Hari ini" : rel == 1 ? "Besok" : rel == -1 ? "Kemarin" : rel > 1 ? $"{rel} hari lagi" : $"{-rel} hari lalu";
        string selKey = DKey(calSel);
        var list = events.Where(e => e.Date == selKey).OrderBy(e => e.Time).ToList();

        CalDayTitle.Text = $"{DayNames[(int)calSel.DayOfWeek]}, {calSel.Day} {MonthNames[calSel.Month - 1]}";
        CalDaySub.Text = list.Count == 0 ? relText : $"{relText} · {list.Count} acara";

        CalEvents.Children.Clear();
        foreach (var ev in list) CalEvents.Children.Add(MakeEventCard(ev));

        if (list.Count == 0)
        {
            var nxt = events.Where(e => string.CompareOrdinal(e.Date, selKey) > 0).OrderBy(e => e.Date).ThenBy(e => e.Time).FirstOrDefault();
            CalEmpty.Text = nxt == null ? "Belum ada acara 🗓\nKetik di bawah untuk menambah" : $"Belum ada acara\nBerikutnya: {ShortDate(nxt.Date)} · {nxt.Title}";
            CalEmpty.Visibility = Visibility.Visible;
        }
        else CalEmpty.Visibility = Visibility.Collapsed;
    }

    UIElement MakeEventCard(CalEvent ev)
    {
        var rest = new SolidColorBrush(Color.FromArgb(0x14, 255, 255, 255));
        var hover = new SolidColorBrush(Color.FromArgb(0x26, 255, 255, 255));
        var when = ParseKey(ev.Date).Add(TimeSpan.TryParse(ev.Time, out var ts) ? ts : TimeSpan.FromHours(23.9));
        var card = new Border
        {
            Height = 36, CornerRadius = new CornerRadius(11), Margin = new Thickness(0, 0, 0, 5), Background = rest,
            BorderBrush = CardEdge, BorderThickness = new Thickness(1), Opacity = when < DateTime.Now ? 0.55 : 1
        };
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

        var bar = new Border
        {
            Width = 3, Height = 18, CornerRadius = new CornerRadius(1.5), HorizontalAlignment = HorizontalAlignment.Center,
            Background = Solid(EvHex[ev.Color % EvHex.Length])
        };
        var time = new TextBlock
        {
            Text = ev.Time == "" ? "Seharian" : ev.Time, FontSize = 11.5, Foreground = Dim, VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold
        };
        var title = new TextBlock
        {
            Text = ev.Title, FontSize = 13.5, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var del = new TextBlock { Text = "✕", FontSize = 11, Foreground = Brushes.White, Opacity = 0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var delHit = new Grid { Background = Brushes.Transparent, ToolTip = "Hapus acara", Cursor = Cursors.Hand };
        delHit.Children.Add(del);
        delHit.MouseLeftButtonUp += (_, e) => { e.Handled = true; events.Remove(ev); SaveEvents(); RenderCalendar(); RenderDay(); };

        Grid.SetColumn(bar, 0); Grid.SetColumn(time, 1); Grid.SetColumn(title, 2); Grid.SetColumn(delHit, 3);
        g.Children.Add(bar); g.Children.Add(time); g.Children.Add(title); g.Children.Add(delHit);
        card.Child = g;
        card.MouseEnter += (_, _) => { card.Background = hover; del.Opacity = 0.75; };
        card.MouseLeave += (_, _) => { card.Background = rest; del.Opacity = 0; };
        return card;
    }

    void AddEvent()
    {
        var raw = CalBox.Text.Trim();
        if (raw == "") return;
        string time = "", title = raw;
        // awalan jam opsional: "19:30 Rapat" atau "7.15 Olahraga"
        var m = Regex.Match(raw, @"^(\d{1,2})[:.](\d{2})\s+(.+)$");
        if (m.Success && int.Parse(m.Groups[1].Value) < 24 && int.Parse(m.Groups[2].Value) < 60)
        {
            time = $"{int.Parse(m.Groups[1].Value):00}:{m.Groups[2].Value}";
            title = m.Groups[3].Value.Trim();
        }
        events.Add(new CalEvent { Date = DKey(calSel), Title = title, Time = time, Color = newEvColor });
        newEvColor = (newEvColor + 1) % EvHex.Length;
        CalColorDot.Fill = Solid(EvHex[newEvColor]);
        CalBox.Clear();
        SaveEvents(); RenderCalendar(); RenderDay();
    }

    void Cal_Prev(object s, RoutedEventArgs e) { calMonth = calMonth.AddMonths(-1); RenderCalendar(); }
    void Cal_Next(object s, RoutedEventArgs e) { calMonth = calMonth.AddMonths(1); RenderCalendar(); }
    void Cal_Today(object s, RoutedEventArgs e)
    {
        calSel = DateTime.Today; calMonth = new DateTime(calSel.Year, calSel.Month, 1);
        RenderCalendar(); RenderDay();
    }
    void Cal_Color(object s, RoutedEventArgs e) { newEvColor = (newEvColor + 1) % EvHex.Length; CalColorDot.Fill = Solid(EvHex[newEvColor]); }
    void Cal_Add(object s, RoutedEventArgs e) => AddEvent();
    void Cal_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddEvent();
        e.Handled = true;
    }
    void Cal_TextChanged(object s, TextChangedEventArgs e) =>
        CalHint.Visibility = CalBox.Text == "" ? Visibility.Visible : Visibility.Collapsed;

    // dipanggil tiap detik: pengingat saat jam acara tiba + ganti hari
    void CalendarTick()
    {
        var now = DateTime.Now;
        string today = DKey(now.Date), hm = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (calToday != today)
        {
            calToday = today;
            RenderCalendar(); RenderDay();
        }
        foreach (var ev in events.Where(e => e.Date == today && e.Time == hm).ToList())
            if (remNotified.Add(today + hm + ev.Title))
                Notify("🗓", ev.Title, "Sekarang · " + hm, "#7C5CFF", 8, "cal");
    }

    // ringkasan sesaat setelah aplikasi dibuka
    void CalendarGreeting()
    {
        string today = DKey(DateTime.Today), tomorrow = DKey(DateTime.Today.AddDays(1));
        var te = events.Where(e => e.Date == today).OrderBy(e => e.Time).ToList();
        if (te.Count > 0)
        {
            var first = te[0];
            Notify("🗓", te.Count == 1 ? "1 acara hari ini" : $"{te.Count} acara hari ini",
                   (first.Time != "" ? first.Time + " · " : "") + first.Title, "#7C5CFF", 6, "cal");
            return;
        }
        var tm = events.Where(e => e.Date == tomorrow).OrderBy(e => e.Time).ToList();
        if (tm.Count > 0)
            Notify("🗓", "Besok: " + tm[0].Title, tm.Count > 1 ? $"+{tm.Count - 1} acara lainnya" : (tm[0].Time != "" ? tm[0].Time : "Seharian"), "#7C5CFF", 6, "cal");
    }

    // ===================== Cuaca (Open-Meteo, tanpa API key) =====================
    double? wxLat, wxLon;
    string wxCity = "";
    DateTime wxAt = DateTime.MinValue;
    bool wxHave;
    int wxTick;

    static (string icon, string text) Wmo(int code, bool day = true) => code switch
    {
        0 => (day ? "☀️" : "🌙", "Cerah"),
        1 => (day ? "🌤️" : "🌙", "Cerah berawan"),
        2 => ("⛅", "Berawan sebagian"),
        3 => ("☁️", "Mendung"),
        45 or 48 => ("🌫️", "Berkabut"),
        >= 51 and <= 57 => ("🌦️", "Gerimis"),
        >= 61 and <= 65 => ("🌧️", "Hujan"),
        66 or 67 => ("🌧️", "Hujan beku"),
        >= 71 and <= 77 => ("🌨️", "Salju"),
        >= 80 and <= 82 => ("🌦️", "Hujan lokal"),
        85 or 86 => ("🌨️", "Hujan salju"),
        >= 95 => ("⛈️", "Badai petir"),
        _ => ("🌡️", "Cuaca")
    };

    static double Num(JsonElement e) => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : 0;
    static string Deg(double v) => (int)Math.Round(v) + "°";

    async Task LoadWeather()
    {
        try
        {
            var pf = Prefs.Current;
            if (wxLat == null || wxCity != pf.City)
            {
                string gu = "https://geocoding-api.open-meteo.com/v1/search?count=1&language=id&format=json&name=" + Uri.EscapeDataString(pf.City);
                using var gd = JsonDocument.Parse(await http.GetStringAsync(gu));
                var r0 = gd.RootElement.GetProperty("results")[0];
                wxLat = r0.GetProperty("latitude").GetDouble();
                wxLon = r0.GetProperty("longitude").GetDouble();
                wxCity = pf.City;
            }
            string url = string.Format(CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}" +
                "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,is_day" +
                "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=auto&forecast_days=5",
                wxLat, wxLon);
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url));
            var root = doc.RootElement;
            var cur = root.GetProperty("current");
            var (icon, text) = Wmo((int)Num(cur.GetProperty("weather_code")), Num(cur.GetProperty("is_day")) > 0);
            WxIcon.Text = icon;
            WxTemp.Text = Deg(Num(cur.GetProperty("temperature_2m")));
            WxDesc.Text = text;
            WxCity.Text = "📍 " + pf.City;
            WxFeels.Text = "Terasa " + Deg(Num(cur.GetProperty("apparent_temperature")));
            WxHum.Text = "💧 " + (int)Math.Round(Num(cur.GetProperty("relative_humidity_2m"))) + "%";
            WxWind.Text = "💨 " + (int)Math.Round(Num(cur.GetProperty("wind_speed_10m"))) + " km/j";

            var daily = root.GetProperty("daily");
            var times = daily.GetProperty("time"); var codes = daily.GetProperty("weather_code");
            var hi = daily.GetProperty("temperature_2m_max"); var lo = daily.GetProperty("temperature_2m_min");
            var rain = daily.GetProperty("precipitation_probability_max");
            WxDays.Children.Clear();
            int n = Math.Min(5, times.GetArrayLength());
            for (int i = 0; i < n; i++)
            {
                var (ic, _) = Wmo((int)Num(codes[i]));
                var day = ParseKey(times[i].GetString()!);
                var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                sp.Children.Add(new TextBlock
                {
                    Text = i == 0 ? "Hari ini" : DayShort3[(int)day.DayOfWeek], FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center,
                    FontWeight = i == 0 ? FontWeights.SemiBold : FontWeights.Normal, Foreground = i == 0 ? Brushes.White : Dim
                });
                sp.Children.Add(new TextBlock { Text = ic, FontFamily = new FontFamily("Segoe UI Emoji"), FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 6) });
                sp.Children.Add(new TextBlock { Text = Deg(Num(hi[i])), FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
                sp.Children.Add(new TextBlock { Text = Deg(Num(lo[i])), FontSize = 11.5, Foreground = Dim, HorizontalAlignment = HorizontalAlignment.Center });
                sp.Children.Add(new TextBlock
                {
                    Text = "💧 " + (int)Math.Round(Num(rain[i])) + "%", FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI"), FontSize = 10,
                    Foreground = Dim, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0)
                });
                WxDays.Children.Add(new Border
                {
                    Margin = new Thickness(3, 0, 3, 0), CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1),
                    Background = i == 0 ? CardHi : CardBg, BorderBrush = i == 0 ? CardHiEdge : CardEdge, Child = sp
                });
            }
            wxHave = true; wxAt = DateTime.Now;
        }
        catch
        {
            if (!wxHave) { WxDesc.Text = "Cuaca tidak tersedia"; WxCity.Text = "Cek koneksi / nama kota di Pengaturan"; }
        }
    }

    // ===================== Sistem =====================
    readonly WPath[] sysArc = new WPath[4];
    readonly TextBlock[] sysPct = new TextBlock[4], sysSub = new TextBlock[4];
    readonly SolidColorBrush[] sysBr = new SolidColorBrush[4];
    readonly List<double> cpuHist = new();
    Polyline sparkLine = null!;
    Polygon sparkFill = null!;
    long netRx, netTx; DateTime netAt; double dlRate, ulRate; bool netInit;
    int sysTickN;
    double diskFrac; string diskSub = "";

    void BuildSystemUi()
    {
        string[] labels = { "CPU", "RAM", "Disk", "Baterai" };
        for (int i = 0; i < 4; i++)
        {
            sysBr[i] = new SolidColorBrush(accentBr.Color);
            var ring = new Grid { Width = 54, Height = 54, HorizontalAlignment = HorizontalAlignment.Center };
            var cv = new Canvas { Width = 54, Height = 54 };
            var track = new Ellipse { Width = 46, Height = 46, Stroke = Solid("#22FFFFFF"), StrokeThickness = 5 };
            Canvas.SetLeft(track, 4); Canvas.SetTop(track, 4);
            sysArc[i] = new WPath { Stroke = sysBr[i], StrokeThickness = 5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            cv.Children.Add(track); cv.Children.Add(sysArc[i]);
            sysPct[i] = new TextBlock { Text = "–", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            ring.Children.Add(cv); ring.Children.Add(sysPct[i]);

            var lbl = new TextBlock { Text = labels[i], FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            sysSub[i] = new TextBlock { FontSize = 9.5, Foreground = Dim, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0) };
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(ring); sp.Children.Add(lbl); sp.Children.Add(sysSub[i]);
            SysTiles.Children.Add(new Border
            {
                Margin = new Thickness(3, 0, 3, 0), CornerRadius = new CornerRadius(16), Background = CardBg,
                BorderBrush = CardEdge, BorderThickness = new Thickness(1), Child = sp
            });
        }
        sparkFill = new Polygon { Fill = accentBr, Opacity = 0.22 };
        sparkLine = new Polyline { Stroke = accentBr, StrokeThickness = 1.8, StrokeLineJoin = PenLineJoin.Round };
        SysSpark.Children.Add(sparkFill); SysSpark.Children.Add(sparkLine);
    }

    static string Rate(double bps)
    {
        if (bps < 1024) return $"{bps:0} B/s";
        if (bps < 1048576) return $"{bps / 1024:0.#} KB/s";
        return $"{bps / 1048576:0.##} MB/s";
    }

    static (long rx, long tx) NetBytes()
    {
        long rx = 0, tx = 0;
        foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType == NetworkInterfaceType.Loopback || n.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
            try { var st = n.GetIPv4Statistics(); rx += st.BytesReceived; tx += st.BytesSent; } catch { }
        }
        return (rx, tx);
    }

    void SetGauge(int i, double frac, string pct, string sub, bool hot, bool good = false)
    {
        sysArc[i].Data = Arc(27, 27, 23, frac);
        sysPct[i].Text = pct;
        sysSub[i].Text = sub;
        sysBr[i].Color = hot ? Col("#FFFF7A85") : good ? Col("#FF4ADE80") : accentBr.Color;
    }

    // dipanggil tiap detik, hanya bekerja saat halaman Sistem terbuka
    void SysTick()
    {
        if (!expanded || page != 6) return;
        sysTickN++;

        double cpu = Native.CpuUsage();
        SetGauge(0, cpu, $"{cpu * 100:0}%", $"{Environment.ProcessorCount} thread", cpu > 0.85);

        var (usedGb, totalGb, mem) = Native.Memory();
        SetGauge(1, mem, $"{mem * 100:0}%", $"{usedGb:0.0} / {totalGb:0} GB", mem > 0.85);

        if (sysTickN % 5 == 1)
        {
            try
            {
                var di = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
                double tot = di.TotalSize / 1073741824.0, used = tot - di.AvailableFreeSpace / 1073741824.0;
                diskFrac = tot > 0 ? used / tot : 0;
                diskSub = $"{used:0} / {tot:0} GB";
            }
            catch { diskFrac = 0; diskSub = "–"; }
        }
        SetGauge(2, diskFrac, $"{diskFrac * 100:0}%", diskSub, diskFrac > 0.9);

        var (has, ac, bat) = Native.Power();
        if (has) SetGauge(3, bat / 100.0, bat + "%", ac ? "Mengisi daya" : "Pakai baterai", !ac && bat <= 20, ac);
        else SetGauge(3, 1, "AC", "Tanpa baterai", false, true);

        var (rx, tx) = NetBytes();
        var nowT = DateTime.UtcNow;
        if (netInit)
        {
            double dt = Math.Max(0.2, (nowT - netAt).TotalSeconds);
            dlRate = Math.Max(0, (rx - netRx) / dt);
            ulRate = Math.Max(0, (tx - netTx) / dt);
        }
        netRx = rx; netTx = tx; netAt = nowT; netInit = true;
        SysDown.Text = Rate(dlRate); SysUp.Text = Rate(ulRate);

        var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
        SysUptime.Text = "Aktif " + (up.TotalDays >= 1 ? $"{(int)up.TotalDays} hr {up.Hours} j" : $"{up.Hours} j {up.Minutes} m");

        // sparkline CPU (40 titik terakhir)
        cpuHist.Add(cpu);
        if (cpuHist.Count > 40) cpuHist.RemoveAt(0);
        double wd = SysSpark.ActualWidth > 10 ? SysSpark.ActualWidth : 300, ht = 34, step = wd / 39.0;
        var pts = new PointCollection();
        for (int i = 0; i < cpuHist.Count; i++)
            pts.Add(new Point(wd - (cpuHist.Count - 1 - i) * step, ht - 2 - cpuHist[i] * (ht - 4)));
        sparkLine.Points = pts;
        var poly = new PointCollection(pts) { new Point(pts[pts.Count - 1].X, ht), new Point(pts[0].X, ht) };
        sparkFill.Points = poly;
    }
}
