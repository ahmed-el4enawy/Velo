using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Velo.Application.Queue;
using Velo.Application.Services;
using Velo.Domain;
using Velo.DownloadEngine;

namespace Velo.Desktop.ViewModels;

public sealed partial class SettingsWindowViewModel : ViewModelBase
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ISpeedThrottler? _throttler;
    private readonly IQueueManager? _queueManager;
    private readonly IClipboardMonitor? _clipboardMonitor;

    [ObservableProperty]
    private int _maxConcurrentDownloads = 3;

    [ObservableProperty]
    private int _segmentsPerDownload = 4;

    [ObservableProperty]
    private int _maxConnectionsPerHost = 8;

    [ObservableProperty]
    private bool _isSpeedLimitEnabled;

    [ObservableProperty]
    private long _speedLimitKb = 2048; // 2 MB/s default when enabled

    [ObservableProperty]
    private bool _clipboardMonitoringEnabled;

    [ObservableProperty]
    private string _defaultDownloadDirectory = string.Empty;

    public event Action? RequestClose;

    public SettingsWindowViewModel(
        ISettingsRepository settingsRepository,
        ISpeedThrottler? throttler = null,
        IQueueManager? queueManager = null,
        IClipboardMonitor? clipboardMonitor = null)
    {
        _settingsRepository = settingsRepository;
        _throttler = throttler;
        _queueManager = queueManager;
        _clipboardMonitor = clipboardMonitor;

        _ = LoadSettingsAsync();
    }

    public async Task LoadSettingsAsync()
    {
        var settings = await _settingsRepository.GetSettingsAsync();

        MaxConcurrentDownloads = settings.MaxConcurrentDownloads;
        SegmentsPerDownload = settings.SegmentsPerDownload;
        MaxConnectionsPerHost = settings.MaxConnectionsPerHost;
        ClipboardMonitoringEnabled = settings.ClipboardMonitoringEnabled;
        DefaultDownloadDirectory = settings.DefaultDownloadDirectory;

        if (settings.SpeedLimitBytesPerSecond.HasValue && settings.SpeedLimitBytesPerSecond.Value > 0)
        {
            IsSpeedLimitEnabled = true;
            SpeedLimitKb = settings.SpeedLimitBytesPerSecond.Value / 1024;
        }
        else
        {
            IsSpeedLimitEnabled = false;
            SpeedLimitKb = 2048;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        long? speedLimitBytes = IsSpeedLimitEnabled && SpeedLimitKb > 0
            ? SpeedLimitKb * 1024
            : null;

        var settings = new AppSettings
        {
            MaxConcurrentDownloads = Math.Clamp(MaxConcurrentDownloads, 1, 10),
            SegmentsPerDownload = Math.Clamp(SegmentsPerDownload, 1, 16),
            MaxConnectionsPerHost = Math.Clamp(MaxConnectionsPerHost, 1, 32),
            SpeedLimitBytesPerSecond = speedLimitBytes,
            ClipboardMonitoringEnabled = ClipboardMonitoringEnabled,
            DefaultDownloadDirectory = DefaultDownloadDirectory
        };

        await _settingsRepository.SaveSettingsAsync(settings);

        // Apply live settings to active runtime components
        if (_throttler is TokenBucketThrottler bucket)
        {
            bucket.SpeedLimitBytesPerSecond = speedLimitBytes;
        }

        if (_queueManager is QueueManager qm)
        {
            qm.MaxConcurrentDownloads = settings.MaxConcurrentDownloads;
            qm.MaxConnectionsPerHost = settings.MaxConnectionsPerHost;
        }

        if (_clipboardMonitor != null)
        {
            _clipboardMonitor.IsEnabled = settings.ClipboardMonitoringEnabled;
        }

        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }
}
