using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace Win11SlideshowPhotos;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
            System.Windows.MessageBox.Show(args.Exception.Message, "Win11SlideshowPhotos Error");
            args.Handled = true;
            Shutdown(1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                WriteCrashLog(ex);
            }
        };

        var arg = e.Args.FirstOrDefault();
        var window = new MainWindow(arg);
        MainWindow = window;
        window.Show();
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Win11SlideshowPhotos");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "crash.log");
            var content = new StringBuilder()
                .AppendLine(DateTime.Now.ToString("u"))
                .AppendLine(ex.ToString())
                .AppendLine(new string('-', 60))
                .ToString();
            File.AppendAllText(path, content);
        }
        catch
        {
        }
    }
}
