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

            // Start background bidirectional IPC server for Chrome extension handoffs
            IpcServer.StartListening(message =>
            {
                if (message == "__ACTIVATE__")
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(ShowMainWindow);
                    return "ACK:ACTIVATED";
                }

                if (Uri.TryCreate(message, UriKind.Absolute, out var validatedUri) &&
                    (validatedUri.Scheme == Uri.UriSchemeHttp || validatedUri.Scheme == Uri.UriSchemeHttps))
                {
                    string safeUrl = validatedUri.ToString();
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        mainVm.OpenAddDownloadDialog(safeUrl);
                    });
                    return "ACK:ACCEPTED";
                }

                return "ACK:REJECTED_INVALID_SCHEME";
            });

            bool startInBackground = false;
            string? initialUrl = null;

            if (desktop.Args != null && desktop.Args.Length > 0)
            {
                for (int i = 0; i < desktop.Args.Length; i++)
                {
                    if (desktop.Args[i] == "--background")
                    {
                        startInBackground = true;
                    }
                    else if (desktop.Args[i] == "--url" && i + 1 < desktop.Args.Length)
                    {
                        initialUrl = desktop.Args[i + 1];
                    }
                }
            }

            if (startInBackground)
            {
                // Start hidden in tray, only show popup dialog for the URL
                desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Minimized;
                desktop.MainWindow.Show();
                desktop.MainWindow.Hide();

                if (!string.IsNullOrWhiteSpace(initialUrl))
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => mainVm.OpenAddDownloadDialog(initialUrl));
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(initialUrl))
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => mainVm.OpenAddDownloadDialog(initialUrl));
                }
            }

            desktop.Exit += (_, _) =>
            {
                IpcServer.Stop();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ShowMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            if (!desktop.MainWindow.IsVisible)
            {
                desktop.MainWindow.Show();
            }
            if (desktop.MainWindow.WindowState == Avalonia.Controls.WindowState.Minimized)
            {
                desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
            }
            desktop.MainWindow.Activate();
        }
    }

    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void OnOpenVeloClicked(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void OnExitVeloClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is MainWindow win)
            {
                win.ForceExit();
            }
            else
            {
                IpcServer.Stop();
                desktop.Shutdown();
            }
        }
    }
}
