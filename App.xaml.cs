using System.Linq;

namespace Win11SlideshowPhotos;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        var arg = e.Args.FirstOrDefault();
        var window = new MainWindow(arg);
        window.Show();
    }
}
