using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Threading;
namespace NexoIsland;

// Semua pengaturan island. Disimpan di %APPDATA%\NexoIsland\settings.json
public class AppSettings
{
    // warna & gradient
    public bool GradientEnabled { get; set; } = true;
    public string Color1 { get; set; } = "#2378FF";
    public string Color2 { get; set; } = "#8B5CF6";
    public bool TwoColors { get; set; } = false;
    public bool SongColor { get; set; } = true;
    public bool Halo { get; set; } = true;

    // tampilan
    public bool Snow { get; set; } = true;
    public bool SnowCaps { get; set; } = true;     // salju menumpuk di tepi jendela & taskbar
    public bool XmasIcons { get; set; } = true;    // kostum Natal di ikon desktop
    public bool Bot { get; set; } = true;
    public bool Hat { get; set; } = true;
    public bool TimeIcon { get; set; } = true;

    // mainan & lampu hias
    public bool AutoRoll { get; set; } = true;            // geser kursor kiri-kanan di tumpukan salju = bola salju terbentuk
    public string LightColor { get; set; } = "multi";     // warm | multi | cool | pink | redgreen
    public string LightPattern { get; set; } = "twinkle"; // steady | blink | chase | twinkle
    public double LightSpeed { get; set; } = 1.0;
    public double LightSpacing { get; set; } = 28;        // jarak antar lampu di kabel jendela (px)
    public double LightSag { get; set; } = 12;            // kabel kendur (px)
    public bool LightLink { get; set; } = true;           // lampu dikaitkan dari satu jendela ke jendela lain (false = hanya di tepi satu jendela)

    // mode musik (layar penuh)
    public bool MusicGlow { get; set; } = true;           // cahaya di tepi layar mengikuti warna cover
    public bool MusicClock { get; set; } = true;          // jam & tanggal di atas layar
    public string GlowMode { get; set; } = "comet";       // comet | pulse | breathe | aurora | rainbow | neon | ambient | spectrum
    public string LyricMode { get; set; } = "classic";    // classic | code | constellation | karaoke | typewriter | neon | wave | scroll | cover

    // notifikasi
    public bool NotifEnabled { get; set; } = true;
    public bool NotifWifi { get; set; } = true;
    public bool NotifBluetooth { get; set; } = true;
    public bool NotifPrayer { get; set; } = true;
    public bool NotifBattery { get; set; } = true;
    public bool NotifCaps { get; set; } = true;
    public bool NotifVolume { get; set; } = true;
    public bool NotifTerminal { get; set; } = true;
    public bool NotifFocus { get; set; } = true;
    public bool NotifXmas { get; set; } = true;
    public bool NotifCalendar { get; set; } = true;

    // jadwal shalat
    public string City { get; set; } = "Jakarta";
    public string Country { get; set; } = "Indonesia";

    [JsonIgnore] public Color C1 => Parse(Color1, Color.FromRgb(0x23, 0x78, 0xFF));
    [JsonIgnore] public Color C2 => Parse(Color2, Color.FromRgb(0x8B, 0x5C, 0xF6));

    public bool Allow(string cat) => NotifEnabled && (cat switch
    {
        "wifi" => NotifWifi,
        "bt" => NotifBluetooth,
        "prayer" => NotifPrayer,
        "battery" => NotifBattery,
        "caps" => NotifCaps,
        "vol" => NotifVolume,
        "term" => NotifTerminal,
        "focus" => NotifFocus,
        "xmas" => NotifXmas,
        "cal" => NotifCalendar,
        _ => true
    });

    public static Color Parse(string? s, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(s!); } catch { return fallback; }
    }
    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}

static class Prefs
{
    // urutan field penting: FilePath harus ada sebelum Current dimuat
    static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NexoIsland", "settings.json");
    public static AppSettings Current { get; private set; } = Load();
    public static event Action? Changed;
    static DispatcherTimer? saveTimer;

    static AppSettings Load()
    {
        try { if (File.Exists(FilePath)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new(); }
        catch { }
        return new();
    }

    // Panggil setelah mengubah Current: semua jendela menerapkan langsung, file disimpan (ditunda sebentar)
    public static void Apply()
    {
        Changed?.Invoke();
        if (saveTimer == null)
        {
            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        }
        saveTimer.Stop(); saveTimer.Start();
    }

    public static void Reset() { Current = new AppSettings(); Apply(); }

    static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

// Rumus gradient island dipakai bersama oleh island & pratinjau di jendela pengaturan
static class Theme
{
    public static Color Mix(Color a, Color b, double t) =>
        Color.FromRgb(Ch(a.R, b.R, t), Ch(a.G, b.G, t), Ch(a.B, b.B, t));

    static byte Ch(byte a, byte b, double t) => (byte)Math.Clamp(Math.Round(a + (b - a) * t), 0, 255);

    // hitam di atas -> warna 1 di tengah -> warna 2 di bawah (1 warna: c2 = c1)
    public static Color Stop68(Color c1) => Mix(Colors.Black, c1, 0.42);
    public static Color Stop84(Color c1, Color c2, bool two) => two ? c1 : Mix(Stop68(c1), c2, 0.5);

    public static LinearGradientBrush Island(Color c1, Color c2, bool two, bool grad)
    {
        var b = new LinearGradientBrush { StartPoint = new System.Windows.Point(0, 0), EndPoint = new System.Windows.Point(0, 1) };
        b.GradientStops.Add(new GradientStop(Colors.Black, 0));
        b.GradientStops.Add(new GradientStop(Colors.Black, 0.28));
        b.GradientStops.Add(new GradientStop(grad ? Stop68(c1) : Colors.Black, 0.68));
        b.GradientStops.Add(new GradientStop(grad ? Stop84(c1, c2, two) : Colors.Black, 0.84));
        b.GradientStops.Add(new GradientStop(grad ? c2 : Colors.Black, 1));
        return b;
    }
}
