namespace Velo.Domain;

/// <summary>
/// Domain entity representing a byte-range chunk allocated to a concurrent socket connection.
/// </summary>
public sealed class DownloadSegment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DownloadId { get; init; }
    public int Index { get; init; }
    public long StartByte { get; init; }
    public long EndByte { get; init; }
    public long NextByte { get; set; }
    public int Attempts { get; set; }
    public SegmentState State { get; set; } = SegmentState.Pending;
    public DateTimeOffset? LastCheckpointAt { get; set; }

    public long TotalBytes => EndByte - StartByte + 1;
    public long DownloadedBytes => Math.Max(0, NextByte - StartByte);
    public bool IsCompleted => NextByte > EndByte;
}
