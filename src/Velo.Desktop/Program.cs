using Avalonia;
using System;

namespace Velo.Desktop;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Velo");
                System.IO.Directory.CreateDirectory(dir);
                string crashPath = System.IO.Path.Combine(dir, "crash.log");
                System.IO.File.AppendAllText(crashPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [AppDomain Unhandled] {e.ExceptionObject}\n\n");
            }
            catch { }
        };

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Velo");
                System.IO.Directory.CreateDirectory(dir);
                string crashPath = System.IO.Path.Combine(dir, "crash.log");
                System.IO.File.AppendAllText(crashPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Main Exception] {ex}\n\n");
            }
            catch { }
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
