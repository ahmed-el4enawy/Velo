using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Velo.Domain;

namespace Velo.DownloadEngine;

/// <summary>
/// Thrown when a remote server returns HTTP 200 OK or refuses byte range headers during segmented downloads.
/// </summary>
public sealed class RangeNotSupportedException : InvalidOperationException
{
    public RangeNotSupportedException(string message) : base(message) { }
}

/// <summary>
/// Worker task responsible for streaming an individual byte-range chunk directly to disk
/// using positional random-access I/O (PRD v2.0 Section 5.2 & FR-03).
/// </summary>
public sealed class SegmentWorker
{
    private readonly HttpClient _httpClient;
    private readonly IPartialFileManager _fileManager;
    private readonly ISpeedThrottler? _throttler;
    private readonly ILogger<SegmentWorker>? _logger;
    private const int BufferSize = 64 * 1024; // 64 KB streaming buffer
    private const int MaxRetries = 5;

    public SegmentWorker(
        HttpClient httpClient,
        IPartialFileManager fileManager,
        ISpeedThrottler? throttler = null,
        ILogger<SegmentWorker>? logger = null)
    {
        _httpClient = httpClient;
        _fileManager = fileManager;
        _throttler = throttler;
        _logger = logger;
    }

    public async Task DownloadSegmentAsync(
        DownloadJob job,
        DownloadSegment segment,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (segment.IsCompleted)
        {
            segment.State = SegmentState.Completed;
            return;
        }

        segment.State = SegmentState.Downloading;
        int retryCount = 0;

        while (segment.NextByte <= segment.EndByte)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, job.EffectiveUrl);
                request.Headers.Range = new RangeHeaderValue(segment.NextByte, segment.EndByte);
                request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
                
                string userAgent = !string.IsNullOrWhiteSpace(job.UserAgent) 
                    ? job.UserAgent 
                    : HttpProbe.DefaultUserAgent;
                request.Headers.UserAgent.ParseAdd(userAgent);

                if (!string.IsNullOrWhiteSpace(job.Cookies))
                {
                    request.Headers.TryAddWithoutValidation("Cookie", job.Cookies);
                }

                if (!string.IsNullOrWhiteSpace(job.Referrer) && Uri.TryCreate(job.Referrer, UriKind.Absolute, out var refUri))
                {
                    request.Headers.Referrer = refUri;
                }

                request.Headers.TryAddWithoutValidation("Accept", "*/*");
                request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

                if (!string.IsNullOrWhiteSpace(job.StrongETag))
                {
                    request.Headers.TryAddWithoutValidation("If-Range", job.StrongETag);
                }

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // Server refused range request and returned full 200 OK
                    throw new RangeNotSupportedException("Server ignored Range request and returned 200 OK. Sequential fallback required.");
                }

                if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    // 416: Check if we are already at or past the end
                    if (segment.NextByte > segment.EndByte)
                    {
                        segment.State = SegmentState.Completed;
                        return;
                    }
                    throw new HttpRequestException($"Server returned 416 Range Not Satisfiable for range {segment.NextByte}-{segment.EndByte}.", null, HttpStatusCode.RequestedRangeNotSatisfiable);
                }

                if ((int)response.StatusCode == 429 || response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    int delaySeconds = 2;
                    if (response.Headers.RetryAfter?.Delta.HasValue == true)
                    {
                        delaySeconds = (int)Math.Clamp(response.Headers.RetryAfter.Delta.Value.TotalSeconds, 1, 60);
                    }
                    _logger?.LogWarning("Server returned {Status} with Retry-After: {Sec}s on segment {Index}", response.StatusCode, delaySeconds, segment.Index);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                    retryCount++;
                    segment.Attempts++;
                    continue;
                }

                if (response.StatusCode != HttpStatusCode.PartialContent)
                {
                    response.EnsureSuccessStatusCode();
                }

                // Strict validation of Content-Range: bytes START-END/TOTAL
                var contentRange = response.Content.Headers.ContentRange;
                if (contentRange != null && contentRange.From.HasValue)
                {
                    if (contentRange.From.Value != segment.NextByte)
                    {
                        throw new InvalidOperationException(
                            $"Server returned mismatched range offset. Expected start at {segment.NextByte}, but got {contentRange.From.Value}.");
                    }
                }

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                byte[] buffer = new byte[BufferSize];

                while (segment.NextByte <= segment.EndByte)
                {
                    int bytesToRead = (int)Math.Min(buffer.Length, segment.EndByte - segment.NextByte + 1);
                    int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken);

                    if (bytesRead == 0)
                    {
                        // Socket EOF reached before expected segment end
                        if (segment.NextByte <= segment.EndByte)
                        {
                            throw new IOException($"Connection dropped prematurely at byte {segment.NextByte} of {segment.EndByte}.");
                        }
                        break;
                    }

                    // Positional asynchronous write to disk
                    await _fileManager.WriteChunkAsync(job.TempPath, segment.NextByte, buffer.AsMemory(0, bytesRead), cancellationToken);

                    if (_throttler != null)
                    {
                        await _throttler.ThrottleAsync(bytesRead, cancellationToken);
                    }

                    segment.NextByte += bytesRead;
                    segment.LastCheckpointAt = DateTimeOffset.UtcNow;
                    progress?.Report(bytesRead);
                }

                // Chunk successfully finished
                segment.State = SegmentState.Completed;
                return;
            }
            catch (RangeNotSupportedException)
            {
                segment.State = SegmentState.Failed;
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && retryCount < MaxRetries)
            {
                retryCount++;
                segment.Attempts++;
                _logger?.LogWarning(ex, "Transient failure in segment {Index} at byte {NextByte}. Retry {Retry}/{Max}",
                    segment.Index, segment.NextByte, retryCount, MaxRetries);

                // Exponential backoff with jitter
                int delayMs = (int)(Math.Pow(2, retryCount) * 500) + Random.Shared.Next(100, 300);
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (Exception)
            {
                segment.State = SegmentState.Failed;
                throw;
            }
        }

        segment.State = SegmentState.Completed;
    }
}
