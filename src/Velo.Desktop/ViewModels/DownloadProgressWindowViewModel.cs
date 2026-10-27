using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Velo.Domain;

namespace Velo.Desktop.ViewModels;

/// <summary>
/// ViewModel for the classic 3-tab IDM download progress popup window.
/// Displays live stream segment progress, speed limiter options, and completion triggers.
/// </summary>
public partial class DownloadProgressWindowViewModel : ViewModelBase
{
    public DownloadItemViewModel Item { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isDetailsExpanded = true;

    [ObservableProperty]
    private string _detailsButtonText = "<< Hide details";

    [ObservableProperty]
    private bool _useSpeedLimiter;

    [ObservableProperty]
    private int _speedLimitKBytes = 1000;

    [ObservableProperty]
    private bool _rememberSpeedLimiter;

    [ObservableProperty]
    private bool _showDownloadCompleteDialog = true;

    [ObservableProperty]
    private bool _hangUpModem;

    [ObservableProperty]
    private bool _exitWhenDone;

    [ObservableProperty]
    private bool _turnOffComputer;

    [ObservableProperty]
    private bool _forceTerminate;

    [ObservableProperty]
    private string _selectedShutdownAction = "Shut down";

    public ObservableCollection<string> ShutdownActions { get; } = ["Shut down", "Sleep", "Hibernate"];

    public string WindowTitle => $"{(int)Item.ProgressPercentage}% {TruncateWithDots(Item.DisplayName, 35)}";
    public string EffectiveUrl => !string.IsNullOrEmpty(Item.EffectiveUrl) ? Item.EffectiveUrl : Item.OriginalUrl;

    public string StatusText => Item.Job.State switch
    {
        JobState.Downloading => "Receiving data...",
        JobState.Completed => "Complete",
        JobState.Paused => "Paused",
        JobState.Queued => "Queued",
        JobState.Failed => "Error",
        _ => Item.Job.State.ToString()
    };

    public string FileSizeText => Item.Job.ExpectedBytes.HasValue
        ? FormatBytes(Item.Job.ExpectedBytes.Value)
        : "Unknown size";

    public string DownloadedText => Item.Job.ExpectedBytes.HasValue
        ? $"{FormatBytes(Item.Job.DownloadedBytes)} ( {Item.ProgressPercentage:0.00} % )"
        : FormatBytes(Item.Job.DownloadedBytes);

    public string TransferRateText => !string.IsNullOrWhiteSpace(Item.SpeedText) && Item.SpeedText != "0 KB/s"
        ? Item.SpeedText
        : (Item.Job.State == JobState.Downloading ? "Calculating..." : "0 KB/s");

    public string TimeLeftText => !string.IsNullOrWhiteSpace(Item.EtaText) && Item.EtaText != "--"
        ? Item.EtaText
        : (Item.Job.State == JobState.Completed ? "Complete" : "--");

    public string ResumeCapabilityText => Item.Job.RangeSupported ? "Yes" : "No";

    public string PauseButtonText => Item.Job.State == JobState.Paused ? "Resume" : "Pause";

    public ObservableCollection<SegmentDisplayModel> Segments => Item.Segments;

    public event Action? RequestClose;

    public DownloadProgressWindowViewModel(DownloadItemViewModel item)
    {
        Item = item;
        Item.PropertyChanged += OnItemPropertyChanged;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(FileSizeText));
        OnPropertyChanged(nameof(DownloadedText));
        OnPropertyChanged(nameof(TransferRateText));
        OnPropertyChanged(nameof(TimeLeftText));
        OnPropertyChanged(nameof(PauseButtonText));
    }

    [RelayCommand]
    private void ToggleDetails()
    {
        IsDetailsExpanded = !IsDetailsExpanded;
        DetailsButtonText = IsDetailsExpanded ? "<< Hide details" : ">> Show details";
    }

    [RelayCommand]
    private async Task PauseResumeAsync()
    {
        if (Item.Job.State == JobState.Paused)
        {
            await Item.ResumeCommand.ExecuteAsync(null);
        }
        else
        {
            await Item.PauseCommand.ExecuteAsync(null);
        }
        OnPropertyChanged(nameof(PauseButtonText));
        OnPropertyChanged(nameof(StatusText));
    }

    [RelayCommand]
    private async Task CancelDownloadAsync()
    {
        await Item.PauseCommand.ExecuteAsync(null);
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void HideTab()
    {
        SelectedTabIndex = 0;
    }

    private static string TruncateWithDots(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
            return text;

        int keep = Math.Max(5, maxLen - 6);
        string ext = Path.GetExtension(text);
        string name = Path.GetFileNameWithoutExtension(text);
        if (name.Length > keep)
        {
            name = name[..keep] + "...";
        }
        return name + ext;
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
        return $"{len:0.###} {sizes[order]}";
    }
}
