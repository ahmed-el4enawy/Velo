using Microsoft.Extensions.DependencyInjection;
using Velo.Application;
using Velo.Application.Queue;
using Velo.Application.Services;
using Velo.Desktop.ViewModels;
using Velo.Domain;
using Velo.DownloadEngine;
using Velo.Infrastructure.Database;
using Velo.Infrastructure.Repositories;

namespace Velo.Desktop;

public static class Bootstrapper
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Database Path in %LOCALAPPDATA%\Velo\velo.db
        string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Velo");
        if (!Directory.Exists(appDataDir))
        {
            Directory.CreateDirectory(appDataDir);
        }

        string dbPath = Path.Combine(appDataDir, "velo.db");
        string connectionString = $"Data Source={dbPath};";

        // Initialize SQLite Schema
        SqliteDatabaseInitializer.Initialize(connectionString);

        // Core Infrastructure & Engine
        var httpClient = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            EnableMultipleHttp2Connections = true,
            UseCookies = false
        })
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        services.AddSingleton(httpClient);
        services.AddSingleton<ISpeedThrottler, TokenBucketThrottler>();
        services.AddSingleton<IHttpProbe, HttpProbe>();
        services.AddSingleton<IPartialFileManager, PositionalFileManager>();
        services.AddSingleton<IDownloadEngine, MultiSegmentEngine>();

        // Repositories & Application Services
        services.AddSingleton<IDownloadRepository>(_ => new SqliteDownloadRepository(connectionString));
        services.AddSingleton<ISettingsRepository>(_ => new SqliteSettingsRepository(connectionString));
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<IQueueManager, QueueManager>();
        services.AddSingleton<IClipboardMonitor, ClipboardMonitorService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<AddDownloadWindowViewModel>();
        services.AddTransient<SettingsWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
