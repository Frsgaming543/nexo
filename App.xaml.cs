using System.Windows;
namespace NexoIsland;
public partial class App : Application
{
    public static SnowWindow? Snow { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Snow = new SnowWindow();
        Snow.Start();   // tampil sesuai pengaturan (salju / salju di jendela / ikon Natal)
        new MainWindow().Show();   // terakhir, supaya island di atas salju
    }
}
