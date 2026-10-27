using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Velo.Application;
using Velo.Desktop.Views;
using Velo.Domain;
using Velo.DownloadEngine;

namespace Velo.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IDownloadService? _downloadService;
    private readonly IServiceProvider? _serviceProvider;
    private readonly ISpeedThrottler? _throttler;
    private long _configuredSpeedLimitBytes = 2048 * 1024; // 2 MB/s default

    [ObservableProperty]
    private string _appTitle = "Velo — Download Accelerator";

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
    private string _statusBarInfo = "Ready";

    [ObservableProperty]
    private DownloadItemViewModel? _selectedDownload;

    [ObservableProperty]
    private bool _isToolbarVisible = true;

    [ObservableProperty]
    private bool _isCategoriesVisible = true;

    [ObservableProperty]
    private string _sortBy = "Date";

    [ObservableProperty]
    private bool _isSpeedLimiterEnabled = false;

    [ObservableProperty]
    private bool _hasFilteredDownloads = false;

    public bool HasDownloads => Downloads.Count > 0;

    public ObservableCollection<DownloadItemViewModel> Downloads { get; } = [];
    public ObservableCollection<DownloadItemViewModel> FilteredDownloads { get; } = [];

    public MainWindowViewModel(IDownloadService downloadService, IServiceProvider serviceProvider)
    {
        _downloadService = downloadService;
        _serviceProvider = serviceProvider;
        _throttler = serviceProvider?.GetService<ISpeedThrottler>();

        if (_downloadService != null)
        {
            _downloadService.DownloadProgressUpdated += OnDownloadProgressUpdated;
            _downloadService.JobStateChanged += OnJobStateChanged;
            DownloadItemViewModel.DetailsRequested += item =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => OpenDownloadProgressDialog(item));
            };
            DownloadItemViewModel.RemoveRequested += async (item, deleteFiles) =>
            {
                if (_downloadService != null)
                {
                    await _downloadService.DeleteDownloadAsync(item.Job.Id, deleteFiles: deleteFiles);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        Downloads.Remove(item);
                        ApplyFilter();
                        SelectedDownload = FilteredDownloads.FirstOrDefault();
                    });
                }
            };
            _ = LoadInitialDownloadsAsync();
            _ = LoadInitialSettingsAsync();
        }
    }

    private async Task LoadInitialSettingsAsync()
    {
        try
        {
            var settingsRepo = _serviceProvider?.GetService<ISettingsRepository>();
            if (settingsRepo != null)
            {
                var settings = await settingsRepo.GetSettingsAsync();
                if (settings.SpeedLimitBytesPerSecond.HasValue && settings.SpeedLimitBytesPerSecond.Value > 0)
                {
                    _configuredSpeedLimitBytes = settings.SpeedLimitBytesPerSecond.Value;
                    IsSpeedLimiterEnabled = true;
                    if (_throttler != null)
                    {
                        _throttler.SpeedLimitBytesPerSecond = _configuredSpeedLimitBytes;
                    }
                }
            }
        }
        catch
        {
            // Fallback to defaults gracefully
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
        SelectedDownload = FilteredDownloads.FirstOrDefault();
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
                item.Job.State = job.State;
                item.UpdateProgress(job.DownloadedBytes, 0);
                ApplyFilter();
                UpdateAggregateMetrics();
            }
        });
    }

    private void UpdateAggregateMetrics()
    {
        int streams = 0;
        int activeCount = 0;
        int completeCount = 0;

        foreach (var item in Downloads)
        {
            if (item.Job.State == JobState.Downloading)
            {
                streams += item.SegmentsCount > 0 ? item.SegmentsCount : 1;
                activeCount++;
            }
            else if (item.Job.State == JobState.Completed)
            {
                completeCount++;
            }
        }

        ActiveStreamsCount = streams;
        StatusBarInfo = $"{Downloads.Count} downloads ({completeCount} complete, {activeCount} downloading)";
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnActiveCategoryChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SelectCategory(string category)
    {
        ActiveCategory = category;
    }

    [RelayCommand]
    public void OpenAddDownloadDialog(string? initialUrl = null)
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

        vm.DownloadStarted += job =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var item = Downloads.FirstOrDefault(d => d.Job.Id == job.Id);
                if (item != null)
                {
                    OpenDownloadProgressDialog(item);
                }
            });
        };

        var dialog = new AddDownloadWindow(vm);
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow != null && desktop.MainWindow.IsVisible)
            {
                dialog.ShowDialog(desktop.MainWindow);
            }
            else
            {
                dialog.Show();
            }
        }
    }

    [RelayCommand]
    public void OpenDownloadProgressDialog(DownloadItemViewModel? item = null)
    {
        item ??= SelectedDownload;
        if (item == null) return;

        var progressVm = new DownloadProgressWindowViewModel(item);
        var progressWindow = new Views.DownloadProgressWindow(progressVm);
        progressWindow.Show();
    }

    public void ExecuteDoubleClickAction(DownloadItemViewModel item)
    {
        switch (item.DoubleClickAction)
        {
            case DoubleClickActionType.OpenFile:
                item.OpenFileCommand.Execute(null);
                break;
            case DoubleClickActionType.OpenProgress:
                OpenDownloadProgressDialog(item);
                break;
            case DoubleClickActionType.OpenFolder:
                item.OpenFolderCommand.Execute(null);
                break;
            case DoubleClickActionType.Properties:
                item.OpenPropertiesCommand.Execute(null);
                break;
        }
    }

    [RelayCommand]
    public void MoveRenameSelected()
    {
        SelectedDownload?.MoveRenameCommand.Execute(null);
    }

    [RelayCommand]
    public void RemoveSelectedWithDialog()
    {
        SelectedDownload?.RemoveCommand.Execute(null);
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

        OpenAddDownloadDialog(null);
    }

    [RelayCommand]
    private async Task ResumeSelectedAsync()
    {
        if (SelectedDownload != null)
        {
            await SelectedDownload.ResumeCommand.ExecuteAsync(null);
        }
        else
        {
            await ResumeAllAsync();
        }
    }

    [RelayCommand]
    private async Task StopSelectedAsync()
    {
        if (SelectedDownload != null)
        {
            await SelectedDownload.PauseCommand.ExecuteAsync(null);
        }
        else
        {
            await PauseAllAsync();
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

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedDownload != null)
        {
            SelectedDownload.RemoveCommand.Execute(null);
            return;
        }

        if (Downloads.Any())
        {
            var first = Downloads.FirstOrDefault();
            first?.RemoveCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task DeleteCompletedAsync()
    {
        if (_downloadService == null) return;
        var completed = Downloads.Where(d => d.Job.State == JobState.Completed).ToList();
        foreach (var item in completed)
        {
            await _downloadService.DeleteDownloadAsync(item.Job.Id, deleteFiles: false);
            Downloads.Remove(item);
        }
        ApplyFilter();
        SelectedDownload = FilteredDownloads.FirstOrDefault();
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
    private void CloseApp()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is MainWindow win)
            {
                win.ForceExit();
            }
            else
            {
                desktop.Shutdown();
            }
        }
    }

    [RelayCommand]
    private void OpenGrabberDialog()
    {
        var dialog = new GrabberWindow(this);
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            dialog.ShowDialog(desktop.MainWindow!);
        }
    }

    [RelayCommand]
    private void ShowAboutDialog()
    {
        var dialog = new AboutWindow();
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            dialog.ShowDialog(desktop.MainWindow!);
        }
    }

    [RelayCommand]
    private async Task RedownloadSelectedAsync()
    {
        if (SelectedDownload != null)
        {
            await SelectedDownload.RedownloadCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private void SetSort(string? criterion)
    {
        if (!string.IsNullOrEmpty(criterion))
        {
            SortBy = criterion;
            ApplyFilter();
        }
    }

    partial void OnIsSpeedLimiterEnabledChanged(bool value)
    {
        if (_throttler != null)
        {
            _throttler.SpeedLimitBytesPerSecond = value && _configuredSpeedLimitBytes > 0
                ? _configuredSpeedLimitBytes
                : null;
        }
    }

    [RelayCommand]
    private void ToggleSpeedLimiter(string? enable)
    {
        if (bool.TryParse(enable, out var state))
        {
            IsSpeedLimiterEnabled = state;
        }
        else
        {
            IsSpeedLimiterEnabled = !IsSpeedLimiterEnabled;
        }
    }

    [RelayCommand]
    private void ToggleCategoriesVisibility()
    {
        IsCategoriesVisible = !IsCategoriesVisible;
    }

    [RelayCommand]
    private void ToggleToolbarVisibility()
    {
        IsToolbarVisible = !IsToolbarVisible;
    }

    [RelayCommand]
    private void FocusSearch()
    {
        // Focus or select search query
        StatusBarInfo = "Search filter activated. Type to find downloads.";
    }

    [RelayCommand]
    private void OpenUrl(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open URL: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task ExportDownloadsAsync(string? format)
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
                if (topLevel != null)
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                    {
                        Title = "Export Downloads List",
                        DefaultExtension = format == "txt" ? "txt" : "ef2",
                        SuggestedFileName = $"velo_downloads_{DateTime.Now:yyyyMMdd}.{format ?? "txt"}"
                    });

                    if (file != null)
                    {
                        var urls = Downloads.Select(d => d.Job.OriginalUrl);
                        await System.IO.File.WriteAllLinesAsync(file.Path.LocalPath, urls);
                        StatusBarInfo = $"Exported {Downloads.Count} downloads to {file.Name}";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusBarInfo = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportDownloadsAsync()
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(desktop.MainWindow);
                if (topLevel != null)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                    {
                        Title = "Import Downloads List",
                        AllowMultiple = false
                    });

                    if (files.Count > 0)
                    {
                        var lines = await System.IO.File.ReadAllLinesAsync(files[0].Path.LocalPath);
                        int count = 0;
                        foreach (var rawLine in lines)
                        {
                            var line = rawLine.Trim();
                            if (line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                line.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            {
                                if (_downloadService != null)
                                {
                                    await _downloadService.CreateDownloadAsync(line);
                                    count++;
                                }
                            }
                        }
                        await LoadInitialDownloadsAsync();
                        StatusBarInfo = $"Imported and scheduled {count} downloads.";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusBarInfo = $"Import failed: {ex.Message}";
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
            "All Downloads" => query,
            "Compressed" => query.Where(d => d.Job.Category == CategoryType.Compressed),
            "Documents" => query.Where(d => d.Job.Category == CategoryType.Documents),
            "Music" => query.Where(d => d.Job.Category == CategoryType.Music),
            "Programs" => query.Where(d => d.Job.Category == CategoryType.Programs),
            "Video" => query.Where(d => d.Job.Category == CategoryType.Video),
            "Unfinished" => query.Where(d => d.Job.State is JobState.Downloading or JobState.Paused or JobState.Queued or JobState.Interrupted),
            "Finished" => query.Where(d => d.Job.State == JobState.Completed),
            "Grabber projects" => query.Where(d => false),
            "Queues" => query.Where(d => d.Job.State == JobState.Queued),
            _ => query
        };

        query = SortBy switch
        {
            "Name" => query.OrderBy(d => d.DisplayName),
            "Size" => query.OrderByDescending(d => d.Job.ExpectedBytes ?? 0),
            "Status" => query.OrderBy(d => d.Job.State),
            "Time Left" => query.OrderBy(d => d.EtaText),
            "Transfer Rate" => query.OrderByDescending(d => d.SpeedText),
            "Date" or _ => query.OrderByDescending(d => d.Job.CreatedAt)
        };

        foreach (var item in query)
        {
            FilteredDownloads.Add(item);
        }

        HasFilteredDownloads = FilteredDownloads.Count > 0;
        OnPropertyChanged(nameof(HasDownloads));
        UpdateAggregateMetrics();
    }
}
