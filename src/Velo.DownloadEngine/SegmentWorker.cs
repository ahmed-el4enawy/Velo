using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Velo.Domain;

namespace Velo.DownloadEngine;

/// <summary>
/// Worker task responsible for streaming an individual byte-range chunk directly to disk
/// using positional random-access I/O (PRD v2.0 Section 5.2 & FR-03).
/// </summary>
public sealed class SegmentWorker
{
    private readonly HttpClient _httpClient;
    private readonly IPartialFileManager _fileManager;
    private readonly ILogger<SegmentWorker>? _logger;
    private const int BufferSize = 64 * 1024; // 64 KB streaming buffer
    private const int MaxRetries = 5;

    public SegmentWorker(HttpClient httpClient, IPartialFileManager fileManager, ILogger<SegmentWorker>? logger = null)
    {
        _httpClient = httpClient;
        _fileManager = fileManager;
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
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Velo/1.0");

                if (!string.IsNullOrWhiteSpace(job.StrongETag))
                {
                    request.Headers.TryAddWithoutValidation("If-Range", job.StrongETag);
                }

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    // Server refused range request and returned full 200 OK
                    throw new InvalidOperationException("Server ignored Range request and returned 200 OK. Sequential fallback required.");
                }

                if (response.StatusCode != HttpStatusCode.PartialContent)
                {
                    response.EnsureSuccessStatusCode();
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

                    segment.NextByte += bytesRead;
                    segment.LastCheckpointAt = DateTimeOffset.UtcNow;
                    progress?.Report(bytesRead);
                }

                // Chunk successfully finished
                segment.State = SegmentState.Completed;
                return;
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
