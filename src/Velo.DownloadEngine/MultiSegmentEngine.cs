using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Velo.Domain;

namespace Velo.DownloadEngine;

/// <summary>
/// High-throughput multi-stream download coordinator implementing Phase 1 P0 requirements (PRD Section 5 & 12).
/// Manages parallel workers, positional file staging, fallback to sequential download, and atomic finalization.
/// </summary>
public sealed class MultiSegmentEngine : IDownloadEngine
{
    private readonly HttpClient _httpClient;
    private readonly IPartialFileManager _fileManager;
    private readonly ISpeedThrottler? _throttler;
    private readonly ILogger<MultiSegmentEngine>? _logger;

    public MultiSegmentEngine(
        HttpClient? httpClient = null,
        IPartialFileManager? fileManager = null,
        ISpeedThrottler? throttler = null,
        ILogger<MultiSegmentEngine>? logger = null)
    {
        _logger = logger;
        _throttler = throttler;
        _fileManager = fileManager ?? new PositionalFileManager();
        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            EnableMultipleHttp2Connections = true
        })
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
    }

    public async Task ExecuteDownloadAsync(
        DownloadJob job,
        IProgress<long>? byteProgress = null,
        CancellationToken cancellationToken = default)
    {
        job.State = JobState.Downloading;
        job.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            // Prepare staging file on disk
            await _fileManager.PrepareStagingFileAsync(job.TempPath, job.ExpectedBytes, cancellationToken);

            if (job.RangeSupported && job.ExpectedBytes is > 0)
            {
                // Multi-threaded segmented download
                if (job.Segments.Count == 0)
                {
                    job.Segments = SegmentPlanner.PlanSegments(job.Id, job.ExpectedBytes.Value, 4);
                }

                var worker = new SegmentWorker(_httpClient, _fileManager, _throttler);
                var workerTasks = job.Segments.Select(segment =>
                    worker.DownloadSegmentAsync(job, segment, byteProgress, cancellationToken));

                await Task.WhenAll(workerTasks);
            }
            else
            {
                // Sequential fallback download (unknown length or server ignores range)
                await ExecuteSequentialDownloadAsync(job, byteProgress, cancellationToken);
            }

            // Verification phase
            job.State = JobState.Verifying;
            job.UpdatedAt = DateTimeOffset.UtcNow;

            var fileInfo = new FileInfo(job.TempPath);
            if (job.ExpectedBytes.HasValue && fileInfo.Length != job.ExpectedBytes.Value)
            {
                throw new InvalidOperationException($"File size mismatch. Expected {job.ExpectedBytes.Value} bytes, but got {fileInfo.Length} bytes.");
            }

            // Atomic rename from .velo.part to target destination
            string finalPath = await _fileManager.FinalizeFileAsync(job.TempPath, job.TargetPath, overwrite: false, cancellationToken);
            job.TargetPath = finalPath;
            job.DownloadedBytes = fileInfo.Length;
            job.State = JobState.Completed;
            job.FinishedAt = DateTimeOffset.UtcNow;
            job.UpdatedAt = DateTimeOffset.UtcNow;
        }
        catch (OperationCanceledException)
        {
            job.State = JobState.Paused;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            throw;
        }
        catch (Exception ex)
        {
            job.State = JobState.Failed;
            job.FailureCode = ex.GetType().Name;
            job.FailureReason = ex.Message;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            _logger?.LogError(ex, "Download job {Id} failed: {Reason}", job.Id, ex.Message);
            throw;
        }
    }

    private async Task ExecuteSequentialDownloadAsync(
        DownloadJob job,
        IProgress<long>? byteProgress,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, job.EffectiveUrl);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Velo/1.0");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(job.TempPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, true);

        byte[] buffer = new byte[64 * 1024];
        int bytesRead;

        while ((bytesRead = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            job.DownloadedBytes += bytesRead;
            byteProgress?.Report(bytesRead);
        }
    }
}
