using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Velo.Desktop.Services;
using Velo.Desktop.ViewModels;
using Velo.Desktop.Views;

namespace Velo.Desktop;

public partial class App : Avalonia.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services = Bootstrapper.ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };

            // Start background IPC server for Chrome extension handoffs
            IpcServer.StartListening(message =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (message == "__ACTIVATE__")
                    {
                        desktop.MainWindow?.Activate();
                    }
                    else if (message.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             message.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        mainVm.OpenAddDownloadDialog(message);
                        desktop.MainWindow?.Activate();
                    }
                });
            });

            // Check command line arguments for initial download URL
            if (desktop.Args != null && desktop.Args.Length > 0)
            {
                for (int i = 0; i < desktop.Args.Length; i++)
                {
                    if (desktop.Args[i] == "--url" && i + 1 < desktop.Args.Length)
                    {
                        string initialUrl = desktop.Args[i + 1];
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => mainVm.OpenAddDownloadDialog(initialUrl));
                        break;
                    }
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
