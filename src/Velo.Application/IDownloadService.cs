using Velo.Domain;

namespace Velo.Application;

public interface IDownloadService
{
    event Action<Guid, long, double>? DownloadProgressUpdated;
    event Action<DownloadJob>? JobStateChanged;

    Task<DownloadJob> CreateDownloadAsync(string url, string? customFileName = null, string? destinationFolder = null, CancellationToken cancellationToken = default);
    Task StartDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default);
    Task PauseDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default);
    Task ResumeDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default);
    Task CancelDownloadAsync(Guid downloadId, bool deletePartialFiles = false, CancellationToken cancellationToken = default);
    Task DeleteDownloadAsync(Guid downloadId, bool deleteFiles = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DownloadJob>> GetAllDownloadsAsync(CancellationToken cancellationToken = default);
    Task RedownloadAsync(Guid downloadId, CancellationToken cancellationToken = default);
    Task UpdateJobAsync(DownloadJob job, CancellationToken cancellationToken = default);
}
