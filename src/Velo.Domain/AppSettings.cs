namespace Velo.Domain;

/// <summary>
/// User configuration settings adhering strictly to PRD v2.0 Section 6 and Section 7.2.
/// </summary>
public sealed class AppSettings
{
    public int MaxConcurrentDownloads { get; set; } = 3;
    public int SegmentsPerDownload { get; set; } = 4;
    public int MaxConnectionsPerHost { get; set; } = 8;
    public int TransientFailureRetries { get; set; } = 5;
    public long? SpeedLimitBytesPerSecond { get; set; } = null; // null = unlimited
    public bool ClipboardMonitoringEnabled { get; set; } = false; // PRD FR-17: explicit opt-in
    public string DefaultDownloadDirectory { get; set; } = string.Empty;
}
