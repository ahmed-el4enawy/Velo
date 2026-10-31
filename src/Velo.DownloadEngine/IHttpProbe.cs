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

public sealed record HttpRequestOptions(
    string? Cookies = null,
    string? Referrer = null,
    string? UserAgent = null);

public interface IHttpProbe
{
    Task<ProbeResult> ProbeUrlAsync(string rawUrl, CancellationToken cancellationToken = default);
    Task<ProbeResult> ProbeUrlAsync(string rawUrl, HttpRequestOptions? options, CancellationToken cancellationToken = default);
}
