using CommunityToolkit.Mvvm.ComponentModel;
using Velo.Domain;

namespace Velo.Desktop.ViewModels;

/// <summary>
/// Presentation model representing an active byte-range download segment.
/// </summary>
public sealed partial class SegmentDisplayModel : ObservableObject
{
    public int Index { get; }
    public string RangeText { get; }
    public string SizeText { get; }
    public long StartByte { get; private set; }
    public long EndByte { get; private set; }
    public long TotalBytes { get; private set; }
    public long DownloadedBytes { get; private set; }

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _stateText;

    [ObservableProperty]
    private string _downloadedText = "0 B";

    [ObservableProperty]
    private string _infoText = "Receiving data...";

    public double ProgressRatio => TotalBytes > 0 ? Math.Clamp((double)DownloadedBytes / TotalBytes, 0.0, 1.0) : 0.0;

    public SegmentDisplayModel(DownloadSegment segment)
    {
        Index = segment.Index + 1;
        StartByte = segment.StartByte;
        EndByte = segment.EndByte;
        TotalBytes = segment.TotalBytes;
        DownloadedBytes = segment.DownloadedBytes;
        RangeText = $"{FormatBytes(segment.StartByte)} - {FormatBytes(segment.EndByte)}";
        SizeText = $"{FormatBytes(segment.TotalBytes)}";
        _downloadedText = FormatBytes(segment.DownloadedBytes);
        _stateText = segment.State.ToString();
        _infoText = segment.State switch
        {
            SegmentState.Downloading => "Receiving data...",
            SegmentState.Completed => "Complete",
            SegmentState.Pending => "Connecting...",
            SegmentState.Failed => "Retrying...",
            _ => segment.State.ToString()
        };
        _progressPercentage = segment.TotalBytes > 0
            ? Math.Clamp((double)segment.DownloadedBytes / segment.TotalBytes * 100.0, 0, 100)
            : 0;
    }

    public void UpdateFromSegment(DownloadSegment segment)
    {
        TotalBytes = segment.TotalBytes;
        DownloadedBytes = segment.DownloadedBytes;
        DownloadedText = FormatBytes(segment.DownloadedBytes);
        StateText = segment.State.ToString();
        InfoText = segment.State switch
        {
            SegmentState.Downloading => "Receiving data...",
            SegmentState.Completed => "Complete",
            SegmentState.Pending => "Connecting...",
            SegmentState.Failed => "Retrying...",
            _ => segment.State.ToString()
        };
        ProgressPercentage = segment.TotalBytes > 0
            ? Math.Clamp((double)segment.DownloadedBytes / segment.TotalBytes * 100.0, 0, 100)
            : 0;
        OnPropertyChanged(nameof(ProgressRatio));
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
