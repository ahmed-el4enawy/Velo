using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Velo.Application;
using Velo.Domain;
using Velo.DownloadEngine;

namespace Velo.Desktop.ViewModels;

public partial class AddDownloadWindowViewModel : ViewModelBase
{
    private readonly IHttpProbe _probe;
    private readonly IDownloadService _downloadService;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _destinationFolder;

    [ObservableProperty]
    private string _statusMessage = "Enter URL and click 'Inspect' to check file size and range support.";

    [ObservableProperty]
    private bool _isProbing;

    [ObservableProperty]
    private bool _canStart;

    public event Action<DownloadJob>? DownloadCreated;

    public AddDownloadWindowViewModel(IHttpProbe probe, IDownloadService downloadService)
    {
        _probe = probe;
        _downloadService = downloadService;
        _destinationFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    // Default parameterless constructor for XAML designer support
    public AddDownloadWindowViewModel() : this(new HttpProbe(), null!)
    {
    }

    [RelayCommand]
    private async Task InspectUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            StatusMessage = "Please enter a valid URL.";
            return;
        }

        IsProbing = true;
        StatusMessage = "Probing server headers and range capability...";

        try
        {
            var result = await _probe.ProbeUrlAsync(Url);
            if (result.IsSuccess)
            {
                FileName = result.ContentDispositionFileName ?? Path.GetFileName(new Uri(result.EffectiveUrl).AbsolutePath);
                if (string.IsNullOrWhiteSpace(FileName))
                {
                    FileName = "download.bin";
                }

                string sizeDisplay = result.ContentLength.HasValue
                    ? FormatBytes(result.ContentLength.Value)
                    : "Unknown size (Stream)";

                string rangeDisplay = result.RangeSupported ? "⚡ Range Supported (Multi-Stream)" : "Sequential (Single Stream)";
                StatusMessage = $"Size: {sizeDisplay} • {rangeDisplay}";
                CanStart = true;
            }
            else
            {
                StatusMessage = $"Probe Error: {result.ErrorMessage}";
                CanStart = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            CanStart = false;
        }
        finally
        {
            IsProbing = false;
        }
    }

    [RelayCommand]
    private async Task StartDownloadAsync()
    {
        if (string.IsNullOrWhiteSpace(Url) || _downloadService == null)
            return;

        try
        {
            var job = await _downloadService.CreateDownloadAsync(Url, FileName, DestinationFolder);
            await _downloadService.StartDownloadAsync(job.Id);
            DownloadCreated?.Invoke(job);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start download: {ex.Message}";
        }
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
}
