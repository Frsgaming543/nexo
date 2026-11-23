using System.Windows;
namespace SnowIsland;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var snow = new SnowWindow();
        snow.Start();   // tampil sesuai pengaturan (salju / salju di jendela / ikon Natal)
        new MainWindow().Show();   // terakhir, supaya island di atas salju
    }
}
