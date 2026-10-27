namespace Velo.Application.Queue;

/// <summary>
/// Manages global and per-host concurrency for download tasks (PRD Section 6 & FR-10).
/// </summary>
public interface IQueueManager
{
    int MaxConcurrentDownloads { get; set; }
    int MaxConnectionsPerHost { get; set; }

    Task EnqueueAsync(Guid downloadId, CancellationToken cancellationToken = default);
    Task NotifyJobStateChangedAsync(Guid downloadId, CancellationToken cancellationToken = default);
    IReadOnlyList<Guid> GetActiveJobIds();
    IReadOnlyList<Guid> GetQueuedJobIds();
}
