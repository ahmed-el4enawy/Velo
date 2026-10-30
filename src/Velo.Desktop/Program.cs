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

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Velo");
                System.IO.Directory.CreateDirectory(dir);
                string crashPath = System.IO.Path.Combine(dir, "crash.log");
                System.IO.File.AppendAllText(crashPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [UnobservedTaskException] {e.Exception}\n\n");
            }
            catch { }
            e.SetObserved();
        };

        const string MutexName = "Global\\Velo_Desktop_SingleInstance_Mutex";
        bool createdNew;
        using var mutex = new System.Threading.Mutex(true, MutexName, out createdNew);

        if (!createdNew)
        {
            // Another instance is already running; forward activation and URL arguments via Named Pipe
            try
            {
                string? urlToSend = null;
                if (args != null && args.Length > 0)
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i] == "--url" && i + 1 < args.Length)
                        {
                            urlToSend = args[i + 1];
                            break;
                        }
                    }
                }

                string messageToSend = string.IsNullOrWhiteSpace(urlToSend) ? "__ACTIVATE__" : urlToSend;
                using var client = new System.IO.Pipes.NamedPipeClientStream(".", Velo.Desktop.Services.IpcServer.DefaultPipeName, System.IO.Pipes.PipeDirection.InOut);
                client.Connect(1500);
                using var writer = new System.IO.StreamWriter(client, System.Text.Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
                writer.WriteLine(messageToSend);
            }
            catch
            {
                // If named pipe forward fails or times out, silently terminate to avoid dual instances
            }
            return;
        }

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
        finally
        {
            try { mutex.ReleaseMutex(); } catch { }
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
