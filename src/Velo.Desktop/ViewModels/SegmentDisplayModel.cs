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

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _stateText;

    public SegmentDisplayModel(DownloadSegment segment)
    {
        Index = segment.Index + 1;
        RangeText = $"{FormatBytes(segment.StartByte)} - {FormatBytes(segment.EndByte)}";
        SizeText = $"{FormatBytes(segment.TotalBytes)}";
        _stateText = segment.State.ToString();
        _progressPercentage = segment.TotalBytes > 0
            ? Math.Clamp((double)segment.DownloadedBytes / segment.TotalBytes * 100.0, 0, 100)
            : 0;
    }

    public void UpdateFromSegment(DownloadSegment segment)
    {
        StateText = segment.State.ToString();
        ProgressPercentage = segment.TotalBytes > 0
            ? Math.Clamp((double)segment.DownloadedBytes / segment.TotalBytes * 100.0, 0, 100)
            : 0;
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
        return $"{len:0.#} {sizes[order]}";
    }
}
