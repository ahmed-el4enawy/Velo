using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Velo.Application;
using Velo.Domain;

namespace Velo.Desktop.ViewModels;

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

        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(TransferRateText));
        OnPropertyChanged(nameof(TimeLeftText));
        OnPropertyChanged(nameof(LastTryDateText));
        OnPropertyChanged(nameof(FormattedSizeOnly));

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

    public static event Action<DownloadItemViewModel>? DetailsRequested;

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
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(TransferRateText));
    }

    [RelayCommand]
    public async Task ResumeAsync()
    {
        await _downloadService.ResumeDownloadAsync(Job.Id);
        StatusText = JobState.Downloading.ToString();
        StatusColor = GetStatusColor(JobState.Downloading);
        OnPropertyChanged(nameof(StatusDisplay));
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        await _downloadService.DeleteDownloadAsync(Job.Id, deleteFiles: false);
    }

    [RelayCommand]
    public void OpenFile()
    {
        if (Job.State == JobState.Completed && File.Exists(Job.TargetPath))
        {
            Process.Start(new ProcessStartInfo(Job.TargetPath) { UseShellExecute = true });
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
        JobState.Downloading => "#10B981", // Emerald Green
        JobState.Completed => "#38BDF8",   // Light Sky Blue
        JobState.Paused => "#F59E0B",      // Amber
        JobState.Failed => "#EF4444",      // Red
        JobState.Canceled => "#64748B",    // Gray
        JobState.Interrupted => "#EC4899", // Pink
        _ => "#94A3B8"                     // Slate
    };
}
