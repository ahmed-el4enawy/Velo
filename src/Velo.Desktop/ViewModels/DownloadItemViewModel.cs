using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Velo.Application;
using Velo.Desktop.Views;
using Velo.Domain;

namespace Velo.Desktop.ViewModels;

public enum DoubleClickActionType
{
    OpenFile,
    OpenProgress,
    OpenFolder,
    Properties
}

public partial class DownloadItemViewModel : ViewModelBase
{
    private readonly IDownloadService _downloadService;

    public DownloadJob Job { get; }

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _host;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _sizeInfo;

    [ObservableProperty]
    private string _speedText = "0 KB/s";

    [ObservableProperty]
    private string _etaText = "--";

    [ObservableProperty]
    private string _statusText;

    [ObservableProperty]
    private string _statusColor;

    [ObservableProperty]
    private bool _isDetailsExpanded;

    [ObservableProperty]
    private string? _queueName;

    [ObservableProperty]
    private DoubleClickActionType _doubleClickAction = DoubleClickActionType.OpenProgress;

    public bool IsDoubleClickOpenFile => DoubleClickAction == DoubleClickActionType.OpenFile;
    public bool IsDoubleClickOpenProgress => DoubleClickAction == DoubleClickActionType.OpenProgress;
    public bool IsDoubleClickOpenFolder => DoubleClickAction == DoubleClickActionType.OpenFolder;
    public bool IsDoubleClickProperties => DoubleClickAction == DoubleClickActionType.Properties;

    public bool CanOpen => Job.State == JobState.Completed || File.Exists(Job.TargetPath);
    public bool CanResume => Job.State is JobState.Paused or JobState.Interrupted or JobState.Failed or JobState.Canceled or JobState.Created;
    public bool CanStop => Job.State is JobState.Downloading or JobState.Queued;
    public bool CanRefreshAddress => Job.State != JobState.Completed;
    public bool CanAddToQueue => Job.State != JobState.Completed;
    public bool CanDeleteFromQueue => Job.State == JobState.Queued || !string.IsNullOrEmpty(QueueName);

    public string OriginalUrl => Job.OriginalUrl;
    public string EffectiveUrl => Job.EffectiveUrl;
    public string TargetPath => Job.TargetPath;
    public string TempPath => Job.TempPath;
    public string RangeSupportText => Job.RangeSupported ? "Supported (HTTP 206 Multi-stream)" : "Unsupported (Sequential Fallback)";
    public string EtagText => string.IsNullOrWhiteSpace(Job.StrongETag) ? "--" : Job.StrongETag;
    public string LastModifiedText => string.IsNullOrWhiteSpace(Job.LastModified) ? "--" : Job.LastModified;
    public string CreatedAtText => Job.CreatedAt.ToLocalTime().ToString("g");
    public string FinishedAtText => Job.FinishedAt?.ToLocalTime().ToString("g") ?? "--";
    public string CategoryText => Job.Category.ToString();
    public string PriorityText => Job.Priority.ToString();
    public int SegmentsCount => Job.Segments.Count;
    public string FailureDetailsText => !string.IsNullOrEmpty(Job.FailureCode)
        ? $"{Job.FailureCode}: {Job.FailureReason}"
        : "None (Clean)";

    public string FileIconEmoji => Job.Category switch
    {
        CategoryType.Compressed => "🗜️",
        CategoryType.Video => "🎬",
        CategoryType.Music => "🎵",
        CategoryType.Documents => "📄",
        CategoryType.Programs => "💻",
        _ => Path.GetExtension(Job.DisplayName).ToLowerInvariant() switch
        {
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" => "🖼️",
            ".pdf" => "📕",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".iso" => "🗜️",
            ".exe" or ".msi" => "💻",
            _ => "📄"
        }
    };

    public string FormattedSizeOnly => Job.ExpectedBytes.HasValue
        ? FormatBytes(Job.ExpectedBytes.Value)
        : (Job.DownloadedBytes > 0 ? FormatBytes(Job.DownloadedBytes) : "--");

    public string LastTryDateText => Job.UpdatedAt.ToLocalTime().ToString("MMM dd HH:mm");
    public string TimeLeftText => EtaText;
    public string TransferRateText => SpeedText;

    public string StatusDisplay => Job.State switch
    {
        JobState.Completed => "Complete",
        JobState.Downloading => "Downloading",
        JobState.Paused => "Paused",
        JobState.Queued => "Queued",
        JobState.Failed => "Error",
        JobState.Interrupted => "Interrupted",
        JobState.Canceled => "Canceled",
        JobState.Verifying => "Verifying",
        _ => Job.State.ToString()
    };

    public ObservableCollection<SegmentDisplayModel> Segments { get; } = [];

    public DownloadItemViewModel(DownloadJob job, IDownloadService downloadService)
    {
        Job = job;
        _downloadService = downloadService;
        _displayName = job.DisplayName;
        _host = job.Host;
        _progressPercentage = job.ProgressPercentage;
        _sizeInfo = FormatSize(job.DownloadedBytes, job.ExpectedBytes);
        _statusText = job.State.ToString();
        _statusColor = GetStatusColor(job.State);

        RefreshSegments();
    }

    public void RefreshSegments()
    {
        Segments.Clear();
        foreach (var seg in Job.Segments)
        {
            Segments.Add(new SegmentDisplayModel(seg));
        }
    }

    public void UpdateProgress(long currentBytes, double speedBytesPerSec)
    {
        Job.DownloadedBytes = currentBytes;
        if (speedBytesPerSec > 0 && Job.State != JobState.Downloading)
        {
            Job.State = JobState.Downloading;
        }

        ProgressPercentage = Job.ProgressPercentage;
        SizeInfo = FormatSize(currentBytes, Job.ExpectedBytes);
        SpeedText = FormatSpeed(speedBytesPerSec);
        StatusText = Job.State.ToString();
        StatusColor = GetStatusColor(Job.State);

        if (speedBytesPerSec > 0 && Job.ExpectedBytes.HasValue)
        {
            long remainingBytes = Math.Max(0, Job.ExpectedBytes.Value - currentBytes);
            double secondsRemaining = remainingBytes / speedBytesPerSec;
            EtaText = FormatEta(TimeSpan.FromSeconds(secondsRemaining));
        }
        else
        {
            EtaText = "--";
        }

        NotifyStateChanges();

        // Update segments if present
        if (Job.Segments.Count > 0)
        {
            if (Segments.Count != Job.Segments.Count)
            {
                RefreshSegments();
            }
            else
            {
                for (int i = 0; i < Job.Segments.Count; i++)
                {
                    Segments[i].UpdateFromSegment(Job.Segments[i]);
                }
            }
        }
    }

    private void NotifyStateChanges()
    {
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(TransferRateText));
        OnPropertyChanged(nameof(TimeLeftText));
        OnPropertyChanged(nameof(LastTryDateText));
        OnPropertyChanged(nameof(FormattedSizeOnly));
        OnPropertyChanged(nameof(CanOpen));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanRefreshAddress));
        OnPropertyChanged(nameof(CanAddToQueue));
        OnPropertyChanged(nameof(CanDeleteFromQueue));
    }

    public static event Action<DownloadItemViewModel>? DetailsRequested;
    public static event Action<DownloadItemViewModel, bool>? RemoveRequested;

    [RelayCommand]
    public void OpenDetails()
    {
        DetailsRequested?.Invoke(this);
    }

    [RelayCommand]
    private void ToggleDetails()
    {
        IsDetailsExpanded = !IsDetailsExpanded;
    }

    [RelayCommand]
    public async Task PauseAsync()
    {
        await _downloadService.PauseDownloadAsync(Job.Id);
        StatusText = JobState.Paused.ToString();
        StatusColor = GetStatusColor(JobState.Paused);
        SpeedText = "0 KB/s";
        NotifyStateChanges();
    }

    [RelayCommand]
    public async Task ResumeAsync()
    {
        await _downloadService.ResumeDownloadAsync(Job.Id);
        StatusText = JobState.Downloading.ToString();
        StatusColor = GetStatusColor(JobState.Downloading);
        NotifyStateChanges();
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        await _downloadService.DeleteDownloadAsync(Job.Id, deleteFiles: false);
    }

    [RelayCommand]
    public void OpenFile()
    {
        if (File.Exists(Job.TargetPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(Job.TargetPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open file: {ex.Message}");
            }
        }
        else
        {
            OpenFolder();
        }
    }

    [RelayCommand]
    public void OpenWith()
    {
        string path = Job.TargetPath;
        if (File.Exists(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo("rundll32.exe", $"shell32.dll,OpenAs_RunDLL \"{path}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open with dialog: {ex.Message}");
            }
        }
        else
        {
            OpenFolder();
        }
    }

    [RelayCommand]
    public void OpenFolder()
    {
        string path = Job.TargetPath;
        if (File.Exists(path))
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start("explorer.exe", $"\"{dir}\"");
            }
        }
    }

    [RelayCommand]
    public void MoveRename()
    {
        var window = new MoveRenameWindow(this, _downloadService);
        ShowDialogWindow(window);
    }

    [RelayCommand]
    public async Task RedownloadAsync()
    {
        await _downloadService.RedownloadAsync(Job.Id);
        UpdateProgress(0, 0);
        StatusText = JobState.Downloading.ToString();
        StatusColor = GetStatusColor(JobState.Downloading);
        NotifyStateChanges();
    }

    [RelayCommand]
    public async Task ResumeDownloadAsync()
    {
        await ResumeAsync();
    }

    [RelayCommand]
    public async Task StopDownloadAsync()
    {
        await PauseAsync();
    }

    [RelayCommand]
    public void RefreshAddress()
    {
        var window = new RefreshAddressWindow(this, _downloadService);
        ShowDialogWindow(window);
    }

    [RelayCommand]
    public void Remove()
    {
        var window = new RemoveDownloadWindow(this, deleteFiles =>
        {
            RemoveRequested?.Invoke(this, deleteFiles);
        });
        ShowDialogWindow(window);
    }

    [RelayCommand]
    public async Task AddToQueueAsync(string? queueName)
    {
        QueueName = queueName ?? "Main download queue";
        if (Job.State != JobState.Downloading && Job.State != JobState.Completed)
        {
            Job.State = JobState.Queued;
            StatusText = JobState.Queued.ToString();
            StatusColor = GetStatusColor(JobState.Queued);
            await _downloadService.UpdateJobAsync(Job);
            NotifyStateChanges();
        }
    }

    [RelayCommand]
    public async Task DeleteFromQueueAsync()
    {
        QueueName = null;
        if (Job.State == JobState.Queued)
        {
            Job.State = JobState.Paused;
            StatusText = JobState.Paused.ToString();
            StatusColor = GetStatusColor(JobState.Paused);
            await _downloadService.UpdateJobAsync(Job);
            NotifyStateChanges();
        }
    }

    [RelayCommand]
    public void SetDoubleClickAction(string? action)
    {
        if (Enum.TryParse<DoubleClickActionType>(action, ignoreCase: true, out var result))
        {
            DoubleClickAction = result;
            OnPropertyChanged(nameof(IsDoubleClickOpenFile));
            OnPropertyChanged(nameof(IsDoubleClickOpenProgress));
            OnPropertyChanged(nameof(IsDoubleClickOpenFolder));
            OnPropertyChanged(nameof(IsDoubleClickProperties));
        }
    }

    [RelayCommand]
    public void OpenProperties()
    {
        var window = new PropertiesWindow(this);
        ShowDialogWindow(window);
    }

    private static void ShowDialogWindow(Window window)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            window.ShowDialog(desktop.MainWindow);
        }
        else
        {
            window.Show();
        }
    }

    private static string FormatSize(long downloaded, long? total)
    {
        string downloadedStr = FormatBytes(downloaded);
        if (total.HasValue)
        {
            return $"{downloadedStr} / {FormatBytes(total.Value)}";
        }
        return $"{downloadedStr} (Stream)";
    }

    private static string FormatSpeed(double bytesPerSec)
    {
        if (bytesPerSec <= 0) return "0 KB/s";
        return $"{FormatBytes((long)bytesPerSec)}/s";
    }

    private static string FormatEta(TimeSpan span)
    {
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m left";
        if (span.TotalMinutes >= 1) return $"{span.Minutes}m {span.Seconds}s left";
        return $"{span.Seconds}s left";
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private static string GetStatusColor(JobState state) => state switch
    {
        JobState.Downloading => "#F59E0B", // Bright Amber Gold
        JobState.Completed => "#10B981",   // Emerald Green
        JobState.Paused => "#D97706",      // Deep Amber Gold
        JobState.Queued => "#FBBF24",      // Light Gold
        JobState.Failed => "#EF4444",      // Red
        JobState.Canceled => "#71717A",    // Muted Zinc Gray
        JobState.Interrupted => "#F43F5E", // Rose Red
        _ => "#A1A1AA"                     // Neutral Zinc
    };
}
