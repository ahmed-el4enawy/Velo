using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Velo.Application;
using Velo.Desktop.Views;
using Velo.Domain;

namespace Velo.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IDownloadService? _downloadService;
    private readonly IServiceProvider? _serviceProvider;

    [ObservableProperty]
    private string _appTitle = "Velo — Next-Gen Download Accelerator";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _activeCategory = "All Downloads";

    [ObservableProperty]
    private long _totalDownloadSpeedBytesPerSec;

    [ObservableProperty]
    private string _totalSpeedText = "0.0 MB/s";

    [ObservableProperty]
    private int _activeStreamsCount;

    [ObservableProperty]
    private DownloadItemViewModel? _selectedDownload;

    public ObservableCollection<DownloadItemViewModel> Downloads { get; } = [];
    public ObservableCollection<DownloadItemViewModel> FilteredDownloads { get; } = [];

    public MainWindowViewModel(IDownloadService downloadService, IServiceProvider serviceProvider)
    {
        _downloadService = downloadService;
        _serviceProvider = serviceProvider;

        if (_downloadService != null)
        {
            _downloadService.DownloadProgressUpdated += OnDownloadProgressUpdated;
            _downloadService.JobStateChanged += OnJobStateChanged;
            _ = LoadInitialDownloadsAsync();
        }
    }

    // Default parameterless constructor for XAML Designer
    public MainWindowViewModel() : this(null!, null!)
    {
    }

    public async Task LoadInitialDownloadsAsync()
    {
        if (_downloadService == null) return;

        var jobs = await _downloadService.GetAllDownloadsAsync();
        Downloads.Clear();
        foreach (var job in jobs)
        {
            var item = new DownloadItemViewModel(job, _downloadService);
            Downloads.Add(item);
        }
        ApplyFilter();
    }

    private void OnDownloadProgressUpdated(Guid downloadId, long currentBytes, double speedBytesPerSec)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var item = Downloads.FirstOrDefault(d => d.Job.Id == downloadId);
            if (item != null)
            {
                item.UpdateProgress(currentBytes, speedBytesPerSec);
                UpdateAggregateMetrics();
            }
        });
    }

    private void OnJobStateChanged(DownloadJob job)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var item = Downloads.FirstOrDefault(d => d.Job.Id == job.Id);
            if (item != null)
            {
                item.UpdateProgress(job.DownloadedBytes, 0);
                ApplyFilter();
                UpdateAggregateMetrics();
            }
        });
    }

    private void UpdateAggregateMetrics()
    {
        int streams = 0;
        foreach (var item in Downloads)
        {
            if (item.Job.State == JobState.Downloading)
            {
                streams += item.SegmentsCount > 0 ? item.SegmentsCount : 1;
            }
        }
        ActiveStreamsCount = streams;
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnActiveCategoryChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SelectCategory(string category)
    {
        if (category == "Settings")
        {
            OpenSettingsDialog();
            return;
        }

        ActiveCategory = category;
    }

    [RelayCommand]
    private void OpenAddDownloadDialog(string? initialUrl = null)
    {
        if (_serviceProvider == null) return;

        var vm = _serviceProvider.GetRequiredService<AddDownloadWindowViewModel>();
        if (!string.IsNullOrWhiteSpace(initialUrl))
        {
            vm.Url = initialUrl;
            _ = vm.InspectUrlCommand.ExecuteAsync(null);
        }

        vm.DownloadCreated += job =>
        {
            var item = new DownloadItemViewModel(job, _downloadService!);
            Downloads.Insert(0, item);
            ApplyFilter();
            SelectedDownload = item;
        };

        var dialog = new AddDownloadWindow(vm);
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            dialog.ShowDialog(desktop.MainWindow!);
        }
    }

    [RelayCommand]
    private async Task PasteAndAddDownloadAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            string? text = await desktop.MainWindow.Clipboard?.GetTextAsync()!;
            if (!string.IsNullOrWhiteSpace(text) &&
                (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                OpenAddDownloadDialog(text.Trim());
                return;
            }
        }

        // If clipboard doesn't contain a direct URL, open the blank dialog
        OpenAddDownloadDialog(null);
    }

    [RelayCommand]
    private void OpenSettingsDialog()
    {
        if (_serviceProvider == null) return;

        var vm = _serviceProvider.GetRequiredService<SettingsWindowViewModel>();
        var dialog = new SettingsWindow(vm);

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            dialog.ShowDialog(desktop.MainWindow!);
        }
    }

    [RelayCommand]
    private async Task PauseAllAsync()
    {
        if (_downloadService == null) return;
        foreach (var d in Downloads.Where(x => x.Job.State == JobState.Downloading))
        {
            await d.PauseCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private async Task ResumeAllAsync()
    {
        if (_downloadService == null) return;
        foreach (var d in Downloads.Where(x => x.Job.State is JobState.Paused or JobState.Interrupted))
        {
            await d.ResumeCommand.ExecuteAsync(null);
        }
    }

    private void ApplyFilter()
    {
        FilteredDownloads.Clear();

        var query = Downloads.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            query = query.Where(d => d.DisplayName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                     d.Host.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        query = ActiveCategory switch
        {
            "Downloading" => query.Where(d => d.Job.State == JobState.Downloading),
            "Queued" => query.Where(d => d.Job.State == JobState.Queued),
            "Paused" => query.Where(d => d.Job.State == JobState.Paused),
            "Completed" => query.Where(d => d.Job.State == JobState.Completed),
            "Failed" => query.Where(d => d.Job.State is JobState.Failed or JobState.Interrupted),
            "Compressed" => query.Where(d => d.Job.Category == CategoryType.Compressed),
            "Video" => query.Where(d => d.Job.Category == CategoryType.Video),
            "Music" => query.Where(d => d.Job.Category == CategoryType.Music),
            "Documents" => query.Where(d => d.Job.Category == CategoryType.Documents),
            "Programs" => query.Where(d => d.Job.Category == CategoryType.Programs),
            _ => query
        };

        foreach (var item in query)
        {
            FilteredDownloads.Add(item);
        }
    }
}
