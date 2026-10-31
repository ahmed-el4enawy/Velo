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
    public const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpProbe>? _logger;

    public HttpProbe(HttpClient? httpClient = null, ILogger<HttpProbe>? logger = null)
    {
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 8,
            AutomaticDecompression = DecompressionMethods.None, // Enforce identity representation for byte mapping
            UseCookies = false // Prevent .NET CookieContainer from stripping our manual Cookie request headers
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public Task<ProbeResult> ProbeUrlAsync(string rawUrl, CancellationToken cancellationToken = default)
    {
        return ProbeUrlAsync(rawUrl, null, cancellationToken);
    }

    public async Task<ProbeResult> ProbeUrlAsync(string rawUrl, HttpRequestOptions? options, CancellationToken cancellationToken = default)
    {
        string normalizedUrl = NormalizeDownloadUrl(rawUrl);

        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri) || 
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

        string userAgent = !string.IsNullOrWhiteSpace(options?.UserAgent) ? options.UserAgent : DefaultUserAgent;

        // Step 1: Try HEAD request (unless Google Drive direct link which often rejects HEAD or returns HTML without download stream)
        bool isGoogleDrive = IsGoogleDriveUrl(uri);
        bool headSucceeded = false;

        if (!isGoogleDrive)
        {
            try
            {
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, uri);
                ApplyHeaders(headRequest, userAgent, options?.Cookies, options?.Referrer);

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
                    headSucceeded = true;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogDebug(ex, "HEAD request failed for {Url}. Falling back to GET Range probe.", rawUrl);
            }
        }

        // Step 2: If HEAD was skipped, failed, or range support is unconfirmed, issue GET Range: bytes=0-0
        if (!headSucceeded || !rangeSupported || contentLength == null)
        {
            try
            {
                using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, effectiveUrl);
                rangeRequest.Headers.Range = new RangeHeaderValue(0, 0);
                ApplyHeaders(rangeRequest, userAgent, options?.Cookies, options?.Referrer);

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
                    var redirectedUri = rangeResponse.RequestMessage?.RequestUri;
                    if (redirectedUri != null && (redirectedUri.Scheme == Uri.UriSchemeHttp || redirectedUri.Scheme == Uri.UriSchemeHttps))
                    {
                        effectiveUrl = redirectedUri.ToString();
                    }

                    // Check if server redirected to Google Sign-in page
                    if (effectiveUrl.Contains("accounts.google.com", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ProbeResult(
                            EffectiveUrl: effectiveUrl,
                            ContentLength: null,
                            ContentDispositionFileName: null,
                            ETag: null,
                            LastModified: null,
                            RangeSupported: false,
                            IsSuccess: false,
                            ErrorMessage: "Download requires Google authentication. Please ensure the extension is reloaded with cookies permission.");
                    }

                    // Check if server returned an HTML webpage rather than a file stream
                    string? mediaType = rangeResponse.Content.Headers.ContentType?.MediaType;
                    if (string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if this is a Google Drive virus scan warning page with a confirmation form
                        if (effectiveUrl.Contains("drive.usercontent.google.com", StringComparison.OrdinalIgnoreCase) ||
                            effectiveUrl.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase) ||
                            effectiveUrl.Contains("docs.google.com", StringComparison.OrdinalIgnoreCase))
                        {
                            string htmlBody = await rangeResponse.Content.ReadAsStringAsync(cancellationToken);
                            
                            // Check for confirmation link / form action / token
                            // E.g.: <form id="download-form" action="https://drive.usercontent.google.com/download" ...>
                            // or <a id="uc-download-link" href="https://drive.usercontent.google.com/download?...">
                            // or hidden input name="confirm" value="..." and name="uuid" value="..."
                            var actionMatch = System.Text.RegularExpressions.Regex.Match(htmlBody, @"action=""([^""]+)""");
                            var confirmMatch = System.Text.RegularExpressions.Regex.Match(htmlBody, @"name=""confirm""\s+value=""([^""]+)""");
                            var uuidMatch = System.Text.RegularExpressions.Regex.Match(htmlBody, @"name=""uuid""\s+value=""([^""]+)""");
                            var idMatch = System.Text.RegularExpressions.Regex.Match(htmlBody, @"name=""id""\s+value=""([^""]+)""");
                            var ucLinkMatch = System.Text.RegularExpressions.Regex.Match(htmlBody, @"id=""uc-download-link""\s+href=""([^""]+)""");

                            string? bypassUrl = null;
                            if (ucLinkMatch.Success)
                            {
                                bypassUrl = System.Net.WebUtility.HtmlDecode(ucLinkMatch.Groups[1].Value);
                            }
                            else if (actionMatch.Success && confirmMatch.Success)
                            {
                                string action = System.Net.WebUtility.HtmlDecode(actionMatch.Groups[1].Value);
                                string confirm = confirmMatch.Groups[1].Value;
                                string uuid = uuidMatch.Success ? $"&uuid={uuidMatch.Groups[1].Value}" : "";
                                string id = idMatch.Success ? $"&id={idMatch.Groups[1].Value}" : "";
                                string sep = action.Contains('?') ? "&" : "?";
                                bypassUrl = $"{action}{sep}confirm={confirm}{uuid}{id}";
                            }

                            if (!string.IsNullOrWhiteSpace(bypassUrl))
                            {
                                if (Uri.TryCreate(new Uri(effectiveUrl), bypassUrl, out var fullBypassUri))
                                {
                                    // Recursive probe with the confirmation bypass URL
                                    return await ProbeUrlAsync(fullBypassUri.ToString(), options, cancellationToken);
                                }
                            }
                        }

                        return new ProbeResult(
                            EffectiveUrl: effectiveUrl,
                            ContentLength: null,
                            ContentDispositionFileName: null,
                            ETag: null,
                            LastModified: null,
                            RangeSupported: false,
                            IsSuccess: false,
                            ErrorMessage: "Server returned a web page (HTML) instead of a file download. Authentication or link confirmation required.");
                    }

                    // Server returned 200 OK to a range request (ranges ignored, fallback to sequential)
                    rangeSupported = false;
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
        string suggestedFileName;
        if (!string.IsNullOrWhiteSpace(contentDispositionFileName))
        {
            suggestedFileName = SafePathValidator.SanitizeFileName(contentDispositionFileName);
        }
        else
        {
            string urlPathName = Path.GetFileName(new Uri(effectiveUrl).AbsolutePath);
            suggestedFileName = !string.IsNullOrWhiteSpace(urlPathName) && !urlPathName.Contains('?') && urlPathName.Contains('.')
                ? SafePathValidator.SanitizeFileName(urlPathName)
                : "download.bin";
        }

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

    public static string NormalizeDownloadUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;

        // Auto-resolve Google Drive links:
        // Case 1: https://drive.google.com/file/d/{id}/view... or /edit
        // Case 2: https://drive.google.com/open?id={id}
        // Case 3: https://drive.google.com/uc?id={id}
        // Case 4: https://docs.google.com/uc?id={id}
        try
        {
            // If the URL was redirected to accounts.google.com signin page with a ?continue=... target, unwrap it
            if (url.Contains("accounts.google.com", StringComparison.OrdinalIgnoreCase))
            {
                var continueMatch = System.Text.RegularExpressions.Regex.Match(
                    url,
                    @"[?&]continue=([^&]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (continueMatch.Success)
                {
                    string unescaped = Uri.UnescapeDataString(continueMatch.Groups[1].Value);
                    if (Uri.TryCreate(unescaped, UriKind.Absolute, out _))
                    {
                        url = unescaped;
                    }
                }
            }

            if (url.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("docs.google.com", StringComparison.OrdinalIgnoreCase))
            {
                string? fileId = null;

                // Match /file/d/{fileId}
                var fileDMatch = System.Text.RegularExpressions.Regex.Match(
                    url,
                    @"/file/d/([a-zA-Z0-9_-]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (fileDMatch.Success)
                {
                    fileId = fileDMatch.Groups[1].Value;
                }
                else
                {
                    // Match ?id={fileId} or &id={fileId}
                    var idQueryMatch = System.Text.RegularExpressions.Regex.Match(
                        url,
                        @"[?&]id=([a-zA-Z0-9_-]+)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (idQueryMatch.Success)
                    {
                        fileId = idQueryMatch.Groups[1].Value;
                    }
                }

                if (!string.IsNullOrWhiteSpace(fileId))
                {
                    // Convert to direct download export link with confirm=t (bypasses virus scan prompt for large files)
                    return $"https://drive.usercontent.google.com/download?id={fileId}&export=download&confirm=t";
                }
            }
        }
        catch
        {
            // Fall back to original url if parsing fails
        }

        return url;
    }

    private static bool IsGoogleDriveUrl(Uri uri)
    {
        return uri.Host.EndsWith("google.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.EndsWith("googleusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyHeaders(HttpRequestMessage request, string userAgent, string? cookies, string? referrer)
    {
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
        request.Headers.UserAgent.ParseAdd(userAgent);

        if (!string.IsNullOrWhiteSpace(cookies))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookies);
        }

        if (!string.IsNullOrWhiteSpace(referrer) && Uri.TryCreate(referrer, UriKind.Absolute, out var refUri))
        {
            request.Headers.Referrer = refUri;
        }

        // Standard browser headers to prevent anti-bot rejection
        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
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
