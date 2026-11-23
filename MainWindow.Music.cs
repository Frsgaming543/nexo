using System.Windows;
using System.Windows.Media;
using Windows.Media.Control;
namespace NexoIsland;

// Cuplikan keadaan musik untuk Mode Musik (layar penuh)
internal class MusicSnap
{
    public string Title = "", Artist = "", LyricState = "";
    public ImageSource? Art;
    public Color Color;
    public bool HasArt, Playing;
    public double Pos, Dur;
    public List<(double t, string l)>? Lyrics;
}

public partial class MainWindow
{
    MusicModeWindow? musicWin;

    // ukuran island saat ini (animasi pegas) -> dipakai cahaya tepi Mode Musik supaya mengikuti bentuk island
    internal (double w, double h) IslandSize() => (Holder.Width, Holder.Height);
    internal IntPtr Hwnd => new System.Windows.Interop.WindowInteropHelper(this).Handle;
    internal void RaiseSelf() { Topmost = false; Topmost = true; }

    internal MusicSnap GetMusicSnap()
    {
        var s = new MusicSnap
        {
            Title = TitleText.Text, Artist = ArtistText.Text, Art = ArtBrush.ImageSource,
            HasArt = hasArt, Color = hasArt ? songBot : BlueGlow, Playing = playing,
            Lyrics = lyrics, LyricState = lyricState
        };
        try
        {
            var ses = mgr?.GetCurrentSession();
            if (ses != null)
            {
                var tl = ses.GetTimelineProperties();
                double pos = tl.Position.TotalSeconds;
                if (playing) pos += (DateTimeOffset.Now - tl.LastUpdatedTime).TotalSeconds;
                double dur = (tl.EndTime - tl.StartTime).TotalSeconds;
                if (dur > 0) pos = Math.Min(pos, dur);
                s.Pos = Math.Max(0, pos); s.Dur = Math.Max(0, dur);
            }
        }
        catch { }
        return s;
    }

    // -1 = sebelumnya, 0 = putar/jeda, 1 = berikutnya
    internal async void MusicCtl(int k)
    {
        try
        {
            var s = mgr!.GetCurrentSession();
            if (k < 0) await s.TrySkipPreviousAsync();
            else if (k == 0) await s.TryTogglePlayPauseAsync();
            else await s.TrySkipNextAsync();
            await Task.Delay(350);
            await Refresh();
        }
        catch { }
    }

    // pindah posisi lagu (tidak semua pemutar mendukung)
    internal async void MusicSeek(double sec)
    {
        try
        {
            var s = mgr!.GetCurrentSession();
            var tl = s.GetTimelineProperties();
            await s.TryChangePlaybackPositionAsync((long)(sec * 10_000_000) + tl.StartTime.Ticks);
        }
        catch { }
    }

    // buka / tutup Mode Musik
    internal void ToggleMusicMode()
    {
        if (musicWin != null) { musicWin.CloseAnimated(); return; }
        musicWin = new MusicModeWindow(this);
        musicWin.Closed += (_, _) => { musicWin = null; App.Snow?.SetMusicOpen(false); };
        App.Snow?.SetMusicOpen(true);
        musicWin.Show();
        // island & salju tetap berada di atas Mode Musik
        Dispatcher.BeginInvoke(new Action(() =>
        {
            App.Snow?.Raise();
            Topmost = false; Topmost = true;
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    void Music_Click(object s, RoutedEventArgs e) => ToggleMusicMode();
    void MusicMode_Menu(object s, RoutedEventArgs e) => ToggleMusicMode();
}
