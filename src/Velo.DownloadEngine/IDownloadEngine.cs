using Velo.Domain;

namespace Velo.DownloadEngine;

public interface IDownloadEngine
{
    Task ExecuteDownloadAsync(
        DownloadJob job,
        IProgress<long>? byteProgress = null,
        CancellationToken cancellationToken = default);
}
