using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace SnowIsland;

// Jendela Pengaturan terpisah (bukan di dalam island). Semua perubahan langsung diterapkan & disimpan.
public partial class SettingsWindow : Window
{
    readonly List<Action> refreshers = new();
    readonly ColorEditor ed1, ed2;

    public SettingsWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Native.DarkTitle(this);

        // warna & gradient
        Bind(SwGrad, p => p.GradientEnabled, (p, v) => p.GradientEnabled = v);
        Bind(SwSong, p => p.SongColor, (p, v) => p.SongColor = v);
        Bind(SwHalo, p => p.Halo, (p, v) => p.Halo = v);
        Bind(SwTwo, p => p.TwoColors, (p, v) => p.TwoColors = v);
        ed1 = new ColorEditor(this, Host1, p => p.Color1, (p, v) => p.Color1 = v);
        ed2 = new ColorEditor(this, Host2, p => p.Color2, (p, v) => p.Color2 = v);

        // tampilan
        Bind(SwSnow, p => p.Snow, (p, v) => p.Snow = v);
        Bind(SwWinSnow, p => p.SnowCaps, (p, v) => p.SnowCaps = v);
        Bind(SwIcons, p => p.XmasIcons, (p, v) => p.XmasIcons = v);
        Bind(SwBot, p => p.Bot, (p, v) => p.Bot = v);
        Bind(SwHat, p => p.Hat, (p, v) => p.Hat = v);
        Bind(SwTime, p => p.TimeIcon, (p, v) => p.TimeIcon = v);
        Bind(SwRoll, p => p.AutoRoll, (p, v) => p.AutoRoll = v);
        BuildLightUi();

        // notifikasi
        Bind(SwNotif, p => p.NotifEnabled, (p, v) => p.NotifEnabled = v);
        Bind(SwWifi, p => p.NotifWifi, (p, v) => p.NotifWifi = v);
        Bind(SwBt, p => p.NotifBluetooth, (p, v) => p.NotifBluetooth = v);
        Bind(SwPrayer, p => p.NotifPrayer, (p, v) => p.NotifPrayer = v);
        Bind(SwBattery, p => p.NotifBattery, (p, v) => p.NotifBattery = v);
        Bind(SwCaps, p => p.NotifCaps, (p, v) => p.NotifCaps = v);
        Bind(SwVol, p => p.NotifVolume, (p, v) => p.NotifVolume = v);
        Bind(SwTerm, p => p.NotifTerminal, (p, v) => p.NotifTerminal = v);
        Bind(SwFocus, p => p.NotifFocus, (p, v) => p.NotifFocus = v);
        Bind(SwXmas, p => p.NotifXmas, (p, v) => p.NotifXmas = v);
        Bind(SwCal, p => p.NotifCalendar, (p, v) => p.NotifCalendar = v);

        Prefs.Changed += OnPrefs;
        Closed += (_, _) => Prefs.Changed -= OnPrefs;
        RefreshAll();
    }

    bool uiBusy;
    static readonly Brush SelBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7B, 0xFF));

    // Pilihan (tombol) & slider untuk lampu hias
    void BuildLightUi()
    {
        AddChoices("Warna lampu", new[] { ("Hangat", "warm"), ("Warna-warni", "multi"), ("Dingin", "cool"), ("Pink", "pink"), ("Merah-hijau", "redgreen") },
                   p => p.LightColor, (p, v) => p.LightColor = v);
        AddChoices("Pola nyala", new[] { ("Diam", "steady"), ("Kedip", "blink"), ("Berjalan", "chase"), ("Berkelip halus", "twinkle") },
                   p => p.LightPattern, (p, v) => p.LightPattern = v);
        AddSlider("Kecepatan", 0.3, 3, p => p.LightSpeed, (p, v) => p.LightSpeed = v, v => v.ToString("0.0") + "x");
        AddSlider("Jarak lampu", 14, 60, p => p.LightSpacing, (p, v) => p.LightSpacing = v, v => v.ToString("0") + " px");
        AddSlider("Kabel kendur", 0, 40, p => p.LightSag, (p, v) => p.LightSag = v, v => v.ToString("0") + " px");
    }

    void AddChoices(string title, (string label, string key)[] items, Func<AppSettings, string> get, Action<AppSettings, string> set)
    {
        LightHost.Children.Add(new TextBlock { Text = title, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 6) });
        var wrap = new WrapPanel();
        var buttons = new List<(Button b, string key)>();
        foreach (var (label, key) in items)
        {
            var btn = new Button { Content = label, Style = (Style)FindResource("Soft"), Margin = new Thickness(0, 0, 8, 8) };
            string k = key;
            btn.Click += (_, _) => { set(Prefs.Current, k); Prefs.Apply(); RefreshChoices(); };
            buttons.Add((btn, key));
            wrap.Children.Add(btn);
        }
        LightHost.Children.Add(wrap);
        void RefreshChoices()
        {
            foreach (var (b, k) in buttons)
            {
                if (get(Prefs.Current) == k) b.Background = SelBrush; else b.ClearValue(Button.BackgroundProperty);
            }
        }
        refreshers.Add(RefreshChoices);
    }

    void AddSlider(string title, double min, double max, Func<AppSettings, double> get, Action<AppSettings, double> set, Func<double, string> fmt)
    {
        var g = new Grid { Margin = new Thickness(0, 4, 0, 2) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        var lbl = new TextBlock { Text = title, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var sl = new Slider { Style = (Style)FindResource("Sld"), Minimum = min, Maximum = max, Foreground = SelBrush };
        var val = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xA3, 0xC4)), TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(sl, 1); Grid.SetColumn(val, 2);
        sl.ValueChanged += (_, _) =>
        {
            val.Text = fmt(sl.Value);
            if (uiBusy) return;
            set(Prefs.Current, sl.Value); Prefs.Apply();
        };
        g.Children.Add(lbl); g.Children.Add(sl); g.Children.Add(val);
        LightHost.Children.Add(g);
        refreshers.Add(() => { uiBusy = true; sl.Value = get(Prefs.Current); val.Text = fmt(sl.Value); uiBusy = false; });
    }

    void Bind(CheckBox cb, Func<AppSettings, bool> get, Action<AppSettings, bool> set)
    {
        refreshers.Add(() => cb.IsChecked = get(Prefs.Current));
        cb.Click += (_, _) => { set(Prefs.Current, cb.IsChecked == true); Prefs.Apply(); };
    }

    void RefreshAll()
    {
        foreach (var r in refreshers) r();
        ed1.Refresh(); ed2.Refresh();
        CityBox.Text = Prefs.Current.City; CountryBox.Text = Prefs.Current.Country;
        UpdatePreview(); UpdateEnabled();
    }

    void OnPrefs() { UpdatePreview(); UpdateEnabled(); }

    // Opsi yang bergantung pada saklar lain dibuat redup
    void UpdateEnabled()
    {
        var p = Prefs.Current;
        SwHalo.IsEnabled = SwTwo.IsEnabled = p.GradientEnabled;
        Host2.IsEnabled = p.GradientEnabled && p.TwoColors;
        Host2.Opacity = Host2.IsEnabled ? 1 : 0.4;
        NotifList.IsEnabled = p.NotifEnabled;
        NotifList.Opacity = p.NotifEnabled ? 1 : 0.4;
    }

    void UpdatePreview()
    {
        var p = Prefs.Current;
        Color c1 = p.C1, c2 = p.TwoColors ? p.C2 : p.C1;
        PvIsland.Background = Theme.Island(c1, c2, p.TwoColors, p.GradientEnabled);

        var bright = Theme.Mix(c2, Colors.White, 0.35);
        var edge = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        edge.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 255, 255, 255), 0));
        edge.GradientStops.Add(new GradientStop(Color.FromArgb(0x1C, 255, 255, 255), 0.4));
        edge.GradientStops.Add(new GradientStop(p.GradientEnabled ? Color.FromArgb(0xE6, bright.R, bright.G, bright.B) : Color.FromArgb(0x55, 255, 255, 255), 1));
        PvIsland.BorderBrush = edge;

        PvIsland.Effect = p.Halo && p.GradientEnabled
            ? new DropShadowEffect { Color = c2, BlurRadius = 34, ShadowDepth = 0, Opacity = 0.7 }
            : null;
    }

    void ApplyCity_Click(object s, RoutedEventArgs e)
    {
        string city = CityBox.Text.Trim(), country = CountryBox.Text.Trim();
        if (city == "" || country == "") { CityNote.Text = "Kota dan negara tidak boleh kosong."; return; }
        var p = Prefs.Current;
        p.City = city; p.Country = country;
        Prefs.Apply();
        CityNote.Text = "Jadwal shalat diperbarui: " + city + ".";
    }

    void Toy_Click(object s, RoutedEventArgs e)
    {
        if (s is Button b && b.Tag is string kind) ToyBox.Add(kind);
    }

    void Reset_Click(object s, RoutedEventArgs e)
    {
        var r = MessageBox.Show(this, "Kembalikan semua pengaturan ke default?", "SnowIsland", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        Prefs.Reset();
        RefreshAll();
        CityNote.Text = "";
    }

    void Close_Click(object s, RoutedEventArgs e) => Close();
}

// Pemilih warna: preset + slider RGB + kode hex. Setiap perubahan langsung diterapkan.
sealed class ColorEditor
{
    static readonly (string name, string hex)[] Presets =
    {
        ("Biru", "#2378FF"), ("Cyan", "#17C3E6"), ("Hijau", "#22C55E"), ("Kuning", "#FACC15"), ("Oranye", "#FF8A2B"),
        ("Merah", "#EF4444"), ("Pink", "#FF4FA3"), ("Ungu", "#8B5CF6"), ("Natal", "#C72626"), ("Putih", "#E5E7EB")
    };

    readonly Func<AppSettings, string> get;
    readonly Action<AppSettings, string> set;
    readonly Slider[] sl = new Slider[3];
    readonly TextBlock[] val = new TextBlock[3];
    readonly TextBox hex = new();
    readonly Border swatch = new();
    readonly List<(Border chip, Color color)> chips = new();
    bool busy;

    public ColorEditor(FrameworkElement res, Panel host, Func<AppSettings, string> get, Action<AppSettings, string> set)
    {
        this.get = get; this.set = set;

        // preset
        var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        foreach (var (name, hx) in Presets)
        {
            var col = AppSettings.Parse(hx, Colors.Blue);
            var chip = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Margin = new Thickness(0, 0, 9, 9),
                Background = new SolidColorBrush(col), BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand, ToolTip = name
            };
            chip.MouseLeftButtonUp += (_, _) => Commit(col, true, true);
            chips.Add((chip, col));
            wrap.Children.Add(chip);
        }
        host.Children.Add(wrap);

        // pratinjau warna + kode hex
        swatch.Width = 32; swatch.Height = 32; swatch.CornerRadius = new CornerRadius(9);
        swatch.BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 255, 255, 255)); swatch.BorderThickness = new Thickness(1);
        hex.Style = (Style)res.FindResource("Dark");
        hex.Width = 104; hex.Padding = new Thickness(9, 5, 9, 5); hex.FontFamily = new FontFamily("Consolas"); hex.MaxLength = 7;
        hex.Margin = new Thickness(10, 0, 0, 0);
        hex.TextChanged += (_, _) =>
        {
            if (busy) return;
            if (TryHex(hex.Text, out var c)) Commit(c, true, false);
        };
        hex.LostKeyboardFocus += (_, _) => { if (!busy) hex.Text = AppSettings.Hex(Current()); };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        row.Children.Add(swatch);
        row.Children.Add(hex);
        row.Children.Add(new TextBlock
        {
            Text = "atau ketik kode hex", Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xA3, 0xC4)), FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0)
        });
        host.Children.Add(row);

        // slider R G B
        string[] names = { "R", "G", "B" };
        string[] tints = { "#FFFF6B6B", "#FF5BE38B", "#FF5AA9FF" };
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });

            var lbl = new TextBlock { Text = names[i], FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(AppSettings.Parse(tints[i], Colors.White)), VerticalAlignment = VerticalAlignment.Center };
            sl[i] = new Slider { Style = (Style)res.FindResource("Sld"), Foreground = new SolidColorBrush(AppSettings.Parse(tints[i], Colors.White)) };
            val[i] = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xA3, 0xC4)), TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(sl[i], 1); Grid.SetColumn(val[i], 2);
            sl[i].ValueChanged += (_, _) =>
            {
                if (busy) return;
                Commit(Color.FromRgb((byte)sl[0].Value, (byte)sl[1].Value, (byte)sl[2].Value), false, true);
            };
            g.Children.Add(lbl); g.Children.Add(sl[i]); g.Children.Add(val[i]);
            host.Children.Add(g);
        }
    }

    Color Current() => AppSettings.Parse(get(Prefs.Current), Colors.Black);

    static bool TryHex(string t, out Color c)
    {
        c = default; t = t.Trim();
        if (!t.StartsWith('#')) t = "#" + t;
        if (t.Length != 7) return false;
        try { c = (Color)ColorConverter.ConvertFromString(t); return true; }
        catch { return false; }
    }

    // tampilkan warna di semua kontrol (tanpa menyimpan)
    void Show(Color c, bool sliders, bool text)
    {
        busy = true;
        if (sliders) { sl[0].Value = c.R; sl[1].Value = c.G; sl[2].Value = c.B; }
        val[0].Text = c.R.ToString(); val[1].Text = c.G.ToString(); val[2].Text = c.B.ToString();
        if (text) hex.Text = AppSettings.Hex(c);
        swatch.Background = new SolidColorBrush(c);
        foreach (var (chip, col) in chips) chip.BorderBrush = col == c ? Brushes.White : Brushes.Transparent;
        busy = false;
    }

    // tampilkan lalu simpan & terapkan ke island
    void Commit(Color c, bool sliders, bool text)
    {
        Show(c, sliders, text);
        set(Prefs.Current, AppSettings.Hex(c));
        Prefs.Apply();
    }

    public void Refresh() => Show(Current(), true, true);
}
