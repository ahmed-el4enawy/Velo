namespace Velo.Domain;

/// <summary>
/// Core domain entity representing a download task, mapped strictly to PRD v2.0 Section 7.2.
/// </summary>
public sealed class DownloadJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string OriginalUrl { get; set; } = string.Empty;
    public string EffectiveUrl { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string TempPath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public long? ExpectedBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public string? StrongETag { get; set; }
    public string? LastModified { get; set; }
    public bool RangeSupported { get; set; }
    public JobState State { get; set; } = JobState.Created;
    public JobPriority Priority { get; set; } = JobPriority.Normal;
    public CategoryType Category { get; set; } = CategoryType.Others;
    public string? FailureCode { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    
    public List<DownloadSegment> Segments { get; set; } = [];

    public double ProgressPercentage =>
        ExpectedBytes is > 0
            ? Math.Clamp((double)DownloadedBytes / ExpectedBytes.Value * 100.0, 0.0, 100.0)
            : 0.0;
}
