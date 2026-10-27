namespace Velo.Domain;

public interface IDownloadRepository
{
    Task AddDownloadAsync(DownloadJob job, CancellationToken cancellationToken = default);
    Task UpdateDownloadAsync(DownloadJob job, CancellationToken cancellationToken = default);
    Task CheckpointSegmentAsync(DownloadSegment segment, CancellationToken cancellationToken = default);
    Task<DownloadJob?> GetDownloadByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DownloadJob>> GetAllDownloadsAsync(CancellationToken cancellationToken = default);
    Task DeleteDownloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReconcileInterruptedDownloadsAsync(CancellationToken cancellationToken = default);
}
