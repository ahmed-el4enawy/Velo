using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Velo.Application;
using Velo.Domain;
using Velo.DownloadEngine;

namespace Velo.Desktop.ViewModels;

/// <summary>
/// ViewModel for the classic IDM "Download File Info" popup dialog.
/// Supports category detection, custom save destinations, file inspection, and direct starting.
/// </summary>
public partial class AddDownloadWindowViewModel : ViewModelBase
{
    private readonly IHttpProbe _probe;
    private readonly IDownloadService _downloadService;
    private readonly string _defaultDownloadsRoot;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _destinationFolder;

    [ObservableProperty]
    private string _saveAsPath = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "General";

    [ObservableProperty]
    private string _categoryFolderPath = string.Empty;

    [ObservableProperty]
    private bool _rememberCategoryPath = true;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _fileSizeDisplay = "Unknown";

    [ObservableProperty]
    private string _fileCategoryIcon = "📁";

    [ObservableProperty]
    private string _statusMessage = "Ready to download.";

    [ObservableProperty]
    private bool _isProbing;

    [ObservableProperty]
    private bool _canStart = true;

    public ObservableCollection<string> Categories { get; } =
        ["Video", "Compressed", "Documents", "Music", "Programs", "General"];

    public event Action<DownloadJob>? DownloadCreated;
    public event Action<DownloadJob>? DownloadStarted;

    public AddDownloadWindowViewModel(IHttpProbe probe, IDownloadService downloadService)
    {
        _probe = probe;
        _downloadService = downloadService;
        _defaultDownloadsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        _destinationFolder = _defaultDownloadsRoot;
        _categoryFolderPath = Path.Combine(_defaultDownloadsRoot, _selectedCategory);
    }

    public AddDownloadWindowViewModel() : this(new HttpProbe(), null!)
    {
    }

    partial void OnUrlChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            _ = InspectUrlAsync();
        }
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        CategoryFolderPath = Path.Combine(_defaultDownloadsRoot, value);
        DestinationFolder = CategoryFolderPath;
        UpdateSaveAsPath();
    }

    partial void OnFileNameChanged(string value)
    {
        UpdateSaveAsPath();
    }

    private void UpdateSaveAsPath()
    {
        string folder = !string.IsNullOrWhiteSpace(DestinationFolder) ? DestinationFolder : _defaultDownloadsRoot;
        SaveAsPath = Path.Combine(folder, !string.IsNullOrWhiteSpace(FileName) ? FileName : "download.bin");
    }

    [RelayCommand]
    public async Task InspectUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            StatusMessage = "Please enter a valid URL.";
            return;
        }

        IsProbing = true;
        StatusMessage = "Inspecting URL...";

        try
        {
            var result = await _probe.ProbeUrlAsync(Url);
            if (result.IsSuccess)
            {
                string detectedName = result.ContentDispositionFileName ?? Path.GetFileName(new Uri(result.EffectiveUrl).AbsolutePath);
                if (string.IsNullOrWhiteSpace(detectedName))
                {
                    detectedName = "download.bin";
                }

                FileName = detectedName;
                CategorizeFile(FileName);

                if (result.ContentLength.HasValue)
                {
                    FileSizeDisplay = FormatBytes(result.ContentLength.Value);
                }
                else
                {
                    FileSizeDisplay = "Unknown (Stream)";
                }

                string rangeDisplay = result.RangeSupported ? "⚡ Multi-Stream Supported" : "Sequential Stream";
                StatusMessage = $"Size: {FileSizeDisplay} • {rangeDisplay}";
                CanStart = true;
            }
            else
            {
                // Fallback from raw URL if server blocked HEAD
                if (Uri.TryCreate(Url, UriKind.Absolute, out var uri))
                {
                    string candidate = Path.GetFileName(uri.AbsolutePath);
                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        FileName = candidate;
                        CategorizeFile(FileName);
                    }
                }
                StatusMessage = $"Connecting directly: {result.ErrorMessage}";
                CanStart = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Note: {ex.Message}";
            CanStart = true;
        }
        finally
        {
            IsProbing = false;
            UpdateSaveAsPath();
        }
    }

    private void CategorizeFile(string filename)
    {
        string ext = Path.GetExtension(filename).ToLowerInvariant();
        switch (ext)
        {
            case ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" or ".flv" or ".wmv":
                SelectedCategory = "Video";
                FileCategoryIcon = "🎬";
                break;
            case ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".iso" or ".bz2":
                SelectedCategory = "Compressed";
                FileCategoryIcon = "📦";
                break;
            case ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt":
                SelectedCategory = "Documents";
                FileCategoryIcon = "📄";
                break;
            case ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a":
                SelectedCategory = "Music";
                FileCategoryIcon = "🎵";
                break;
            case ".exe" or ".msi" or ".bat" or ".cmd":
                SelectedCategory = "Programs";
                FileCategoryIcon = "💻";
                break;
            default:
                SelectedCategory = "General";
                FileCategoryIcon = "📁";
                break;
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
            DownloadStarted?.Invoke(job);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start download: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DownloadLaterAsync()
    {
        if (string.IsNullOrWhiteSpace(Url) || _downloadService == null)
            return;

        try
        {
            var job = await _downloadService.CreateDownloadAsync(Url, FileName, DestinationFolder);
            DownloadCreated?.Invoke(job);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save download: {ex.Message}";
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
