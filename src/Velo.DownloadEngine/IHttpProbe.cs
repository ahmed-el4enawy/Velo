namespace Velo.DownloadEngine;

public sealed record ProbeResult(
    string EffectiveUrl,
    long? ContentLength,
    string? ContentDispositionFileName,
    string? ETag,
    string? LastModified,
    bool RangeSupported,
    bool IsSuccess,
    string? ErrorMessage);

public interface IHttpProbe
{
    Task<ProbeResult> ProbeUrlAsync(string rawUrl, CancellationToken cancellationToken = default);
}
