using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Velo.Domain;

namespace Velo.DownloadEngine;

/// <summary>
/// Probes remote HTTP/HTTPS resources to inspect size, range capability, and metadata
/// as defined in PRD v2.0 Section 5.1.
/// </summary>
public sealed class HttpProbe : IHttpProbe
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpProbe>? _logger;

    public HttpProbe(HttpClient? httpClient = null, ILogger<HttpProbe>? logger = null)
    {
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = DecompressionMethods.None // Enforce identity representation for byte mapping
        })
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<ProbeResult> ProbeUrlAsync(string rawUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) || 
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new ProbeResult(
                EffectiveUrl: rawUrl,
                ContentLength: null,
                ContentDispositionFileName: null,
                ETag: null,
                LastModified: null,
                RangeSupported: false,
                IsSuccess: false,
                ErrorMessage: "Invalid or unsupported URL scheme. Only HTTP and HTTPS are permitted.");
        }

        string effectiveUrl = uri.ToString();
        long? contentLength = null;
        string? contentDispositionFileName = null;
        string? etag = null;
        string? lastModified = null;
        bool rangeSupported = false;

        // Step 1: Try HEAD request
        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, uri);
            headRequest.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
            headRequest.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Velo/1.0");

            using var headResponse = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (headResponse.IsSuccessStatusCode)
            {
                var redirectedUri = headResponse.RequestMessage?.RequestUri;
                if (redirectedUri != null && (redirectedUri.Scheme == Uri.UriSchemeHttp || redirectedUri.Scheme == Uri.UriSchemeHttps))
                {
                    effectiveUrl = redirectedUri.ToString();
                }
                contentLength = headResponse.Content.Headers.ContentLength;
                etag = headResponse.Headers.ETag?.Tag;
                lastModified = headResponse.Content.Headers.LastModified?.ToString("R");
                contentDispositionFileName = ExtractFileNameFromDisposition(headResponse.Content.Headers.ContentDisposition);

                if (headResponse.Headers.AcceptRanges.Contains("bytes"))
                {
                    rangeSupported = true;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogDebug(ex, "HEAD request failed for {Url}. Falling back to GET Range probe.", rawUrl);
        }

        // Step 2: If range support is unconfirmed or HEAD returned non-success, issue GET Range: bytes=0-0
        if (!rangeSupported || contentLength == null)
        {
            try
            {
                using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, effectiveUrl);
                rangeRequest.Headers.Range = new RangeHeaderValue(0, 0);
                rangeRequest.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
                rangeRequest.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Velo/1.0");

                using var rangeResponse = await _httpClient.SendAsync(rangeRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (rangeResponse.StatusCode == HttpStatusCode.PartialContent)
                {
                    // Strict HTTP 206 validation: Verify Content-Range header
                    var contentRange = rangeResponse.Content.Headers.ContentRange;
                    if (contentRange != null && contentRange.Unit.Equals("bytes", StringComparison.OrdinalIgnoreCase))
                    {
                        rangeSupported = true;
                        var redirectedUri = rangeResponse.RequestMessage?.RequestUri;
                        if (redirectedUri != null && (redirectedUri.Scheme == Uri.UriSchemeHttp || redirectedUri.Scheme == Uri.UriSchemeHttps))
                        {
                            effectiveUrl = redirectedUri.ToString();
                        }
                        etag ??= rangeResponse.Headers.ETag?.Tag;
                        lastModified ??= rangeResponse.Content.Headers.LastModified?.ToString("R");
                        contentDispositionFileName ??= ExtractFileNameFromDisposition(rangeResponse.Content.Headers.ContentDisposition);

                        if (contentRange.HasLength)
                        {
                            contentLength = contentRange.Length;
                        }
                    }
                    else
                    {
                        // 206 returned without valid Content-Range header
                        rangeSupported = false;
                        var redirectedUri = rangeResponse.RequestMessage?.RequestUri;
                        if (redirectedUri != null && (redirectedUri.Scheme == Uri.UriSchemeHttp || redirectedUri.Scheme == Uri.UriSchemeHttps))
                        {
                            effectiveUrl = redirectedUri.ToString();
                        }
                        contentLength ??= rangeResponse.Content.Headers.ContentLength;
                        etag ??= rangeResponse.Headers.ETag?.Tag;
                        lastModified ??= rangeResponse.Content.Headers.LastModified?.ToString("R");
                        contentDispositionFileName ??= ExtractFileNameFromDisposition(rangeResponse.Content.Headers.ContentDisposition);
                    }
                }
                else if (rangeResponse.IsSuccessStatusCode)
                {
                    // Server returned 200 OK to a range request (ranges ignored, fallback to sequential)
                    rangeSupported = false;
                    var redirectedUri = rangeResponse.RequestMessage?.RequestUri;
                    if (redirectedUri != null && (redirectedUri.Scheme == Uri.UriSchemeHttp || redirectedUri.Scheme == Uri.UriSchemeHttps))
                    {
                        effectiveUrl = redirectedUri.ToString();
                    }
                    contentLength ??= rangeResponse.Content.Headers.ContentLength;
                    etag ??= rangeResponse.Headers.ETag?.Tag;
                    lastModified ??= rangeResponse.Content.Headers.LastModified?.ToString("R");
                    contentDispositionFileName ??= ExtractFileNameFromDisposition(rangeResponse.Content.Headers.ContentDisposition);
                }
                else if (rangeResponse.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    // 416 Range Not Satisfiable
                    rangeSupported = false;
                    var redirectedUri = rangeResponse.RequestMessage?.RequestUri;
                    if (redirectedUri != null && (redirectedUri.Scheme == Uri.UriSchemeHttp || redirectedUri.Scheme == Uri.UriSchemeHttps))
                    {
                        effectiveUrl = redirectedUri.ToString();
                    }
                    etag ??= rangeResponse.Headers.ETag?.Tag;
                    lastModified ??= rangeResponse.Content.Headers.LastModified?.ToString("R");
                    if (rangeResponse.Content.Headers.ContentRange?.HasLength == true)
                    {
                        contentLength = rangeResponse.Content.Headers.ContentRange.Length;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "GET Range probe failed for {Url}", effectiveUrl);
                if (contentLength == null)
                {
                    return new ProbeResult(
                        EffectiveUrl: effectiveUrl,
                        ContentLength: null,
                        ContentDispositionFileName: null,
                        ETag: null,
                        LastModified: null,
                        RangeSupported: false,
                        IsSuccess: false,
                        ErrorMessage: $"Network connection error: {ex.Message}");
                }
            }
        }

        // Determine sanitized filename from disposition or URL path
        string suggestedFileName = !string.IsNullOrWhiteSpace(contentDispositionFileName)
            ? SafePathValidator.SanitizeFileName(contentDispositionFileName)
            : SafePathValidator.SanitizeFileName(Path.GetFileName(new Uri(effectiveUrl).AbsolutePath));

        return new ProbeResult(
            EffectiveUrl: effectiveUrl,
            ContentLength: contentLength,
            ContentDispositionFileName: suggestedFileName,
            ETag: etag,
            LastModified: lastModified,
            RangeSupported: rangeSupported,
            IsSuccess: true,
            ErrorMessage: null);
    }

    private static string? ExtractFileNameFromDisposition(ContentDispositionHeaderValue? disposition)
    {
        if (disposition == null) return null;

        // filename* takes precedence (RFC 5987 / 6266)
        if (!string.IsNullOrWhiteSpace(disposition.FileNameStar))
        {
            string raw = disposition.FileNameStar;
            // Format can be UTF-8''encoded_name.ext
            int tickIdx = raw.LastIndexOf("''", StringComparison.Ordinal);
            if (tickIdx >= 0 && tickIdx + 2 < raw.Length)
            {
                raw = raw[(tickIdx + 2)..];
            }
            return Uri.UnescapeDataString(raw.Trim('"'));
        }

        if (!string.IsNullOrWhiteSpace(disposition.FileName))
        {
            return Uri.UnescapeDataString(disposition.FileName.Trim('"'));
        }

        return null;
    }
}
