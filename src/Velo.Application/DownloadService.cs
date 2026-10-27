using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Velo.Domain;
using Velo.DownloadEngine;

namespace Velo.Application;

/// <summary>
/// Core application service coordinating URL probing, job creation, queue execution,
/// pausing, resuming, and persistence (PRD v2.0 Section 7).
/// </summary>
public sealed class DownloadService : IDownloadService
{
    private readonly IHttpProbe _probe;
    private readonly IDownloadEngine _engine;
    private readonly IDownloadRepository _repository;
    private readonly ILogger<DownloadService>? _logger;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeCancellations = new();

    public int DefaultSegmentCount { get; set; } = 8;

    public event Action<Guid, long, double>? DownloadProgressUpdated;
    public event Action<DownloadJob>? JobStateChanged;

    public DownloadService(
        IHttpProbe probe,
        IDownloadEngine engine,
        IDownloadRepository repository,
        ILogger<DownloadService>? logger = null,
        int defaultSegmentCount = 8)
    {
        DefaultSegmentCount = Math.Clamp(defaultSegmentCount, 1, 32);
        _probe = probe;
        _engine = engine;
        _repository = repository;
        _logger = logger;
    }

    public async Task<DownloadJob> CreateDownloadAsync(
        string url,
        string? customFileName = null,
        string? destinationFolder = null,
        CancellationToken cancellationToken = default)
    {
        var probeResult = await _probe.ProbeUrlAsync(url, cancellationToken);
        if (!probeResult.IsSuccess)
        {
            throw new InvalidOperationException($"Unable to probe download URL: {probeResult.ErrorMessage}");
        }

        string fileName = !string.IsNullOrWhiteSpace(customFileName)
            ? SafePathValidator.SanitizeFileName(customFileName)
            : probeResult.ContentDispositionFileName ?? "download.bin";

        var category = FileCategoryClassifier.Classify(fileName);
        string defaultFolder = !string.IsNullOrWhiteSpace(destinationFolder)
            ? SafePathValidator.SanitizeDestinationDirectory(destinationFolder)
            : FileCategoryClassifier.ResolveCategoryFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), category);

        string targetPath = SafePathValidator.CombineSafePath(defaultFolder, fileName);
        string tempPath = targetPath + ".velo.part";

        var uri = new Uri(probeResult.EffectiveUrl);

        var job = new DownloadJob
        {
            OriginalUrl = url,
            EffectiveUrl = probeResult.EffectiveUrl,
            DisplayName = fileName,
            Host = uri.Host,
            TargetPath = targetPath,
            TempPath = tempPath,
            ExpectedBytes = probeResult.ContentLength,
            StrongETag = probeResult.ETag,
            LastModified = probeResult.LastModified,
            RangeSupported = probeResult.RangeSupported,
            Category = category,
            State = JobState.Created
        };

        if (job.RangeSupported && job.ExpectedBytes is > 0)
        {
            job.Segments = SegmentPlanner.PlanSegments(job.Id, job.ExpectedBytes.Value, DefaultSegmentCount);
        }

        await _repository.AddDownloadAsync(job, cancellationToken);
        JobStateChanged?.Invoke(job);
        return job;
    }

    public async Task StartDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetDownloadByIdAsync(downloadId, cancellationToken);
        if (job == null)
        {
            throw new KeyNotFoundException($"Download job with ID {downloadId} was not found.");
        }

        if (job.State == JobState.Completed)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        if (!_activeCancellations.TryAdd(downloadId, cts))
        {
            // Already running
            return;
        }

        // Fire and coordinate download task in background
        _ = Task.Run(async () =>
        {
            long lastReportTimestamp = Stopwatch.GetTimestamp();
            long lastCheckpointTimestamp = Stopwatch.GetTimestamp();
            long bytesInInterval = 0;

            try
            {
                var progress = new Progress<long>(chunkBytes =>
                {
                    Interlocked.Add(ref bytesInInterval, chunkBytes);

                    // If segments exist, calculate downloaded bytes from segments for accuracy
                    if (job.Segments.Count > 0)
                    {
                        job.DownloadedBytes = job.Segments.Sum(s => s.DownloadedBytes);
                    }
                    else
                    {
                        job.DownloadedBytes += chunkBytes;
                    }

                    long now = Stopwatch.GetTimestamp();
                    double elapsedSec = (double)(now - lastReportTimestamp) / Stopwatch.Frequency;
                    if (elapsedSec >= 0.2) // 5 times/sec (PRD Section 11: 4-10 times/sec)
                    {
                        long delta = Interlocked.Exchange(ref bytesInInterval, 0);
                        double currentSpeed = delta / Math.Max(0.001, elapsedSec);
                        lastReportTimestamp = now;

                        DownloadProgressUpdated?.Invoke(job.Id, job.DownloadedBytes, currentSpeed);
                    }

                    // Periodic database checkpointing every 2 seconds during active streaming
                    double checkpointElapsedSec = (double)(now - lastCheckpointTimestamp) / Stopwatch.Frequency;
                    if (checkpointElapsedSec >= 2.0)
                    {
                        lastCheckpointTimestamp = now;
                        _ = _repository.UpdateDownloadAsync(job, CancellationToken.None);
                    }
                });

                job.State = JobState.Downloading;
                JobStateChanged?.Invoke(job);

                await _engine.ExecuteDownloadAsync(job, progress, cts.Token);
                await _repository.UpdateDownloadAsync(job, CancellationToken.None);
                JobStateChanged?.Invoke(job);
            }
            catch (OperationCanceledException)
            {
                job.State = JobState.Paused;
                if (job.Segments.Count > 0)
                {
                    job.DownloadedBytes = job.Segments.Sum(s => s.DownloadedBytes);
                }
                await _repository.UpdateDownloadAsync(job, CancellationToken.None);
                JobStateChanged?.Invoke(job);
            }
            catch (Exception ex)
            {
                job.State = JobState.Failed;
                job.FailureCode = ex.GetType().Name;
                job.FailureReason = ex.Message;
                if (job.Segments.Count > 0)
                {
                    job.DownloadedBytes = job.Segments.Sum(s => s.DownloadedBytes);
                }
                await _repository.UpdateDownloadAsync(job, CancellationToken.None);
                JobStateChanged?.Invoke(job);
            }
            finally
            {
                _activeCancellations.TryRemove(downloadId, out _);
                cts.Dispose();
            }
        }, CancellationToken.None);
    }

    public async Task PauseDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default)
    {
        if (_activeCancellations.TryGetValue(downloadId, out var cts))
        {
            cts.Cancel();
        }
        else
        {
            var job = await _repository.GetDownloadByIdAsync(downloadId, cancellationToken);
            if (job != null && job.State == JobState.Downloading)
            {
                job.State = JobState.Paused;
                await _repository.UpdateDownloadAsync(job, cancellationToken);
                JobStateChanged?.Invoke(job);
            }
        }
    }

    public async Task ResumeDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default)
    {
        await StartDownloadAsync(downloadId, cancellationToken);
    }

    public async Task CancelDownloadAsync(Guid downloadId, bool deletePartialFiles = false, CancellationToken cancellationToken = default)
    {
        if (_activeCancellations.TryRemove(downloadId, out var cts))
        {
            cts.Cancel();
        }

        var job = await _repository.GetDownloadByIdAsync(downloadId, cancellationToken);
        if (job != null)
        {
            job.State = JobState.Canceled;
            await _repository.UpdateDownloadAsync(job, cancellationToken);
            JobStateChanged?.Invoke(job);

            if (deletePartialFiles && File.Exists(job.TempPath))
            {
                try { File.Delete(job.TempPath); } catch { /* Ignore file lock */ }
            }
        }
    }

    public async Task DeleteDownloadAsync(Guid downloadId, bool deleteFiles = false, CancellationToken cancellationToken = default)
    {
        await CancelDownloadAsync(downloadId, deletePartialFiles: deleteFiles, cancellationToken);
        var job = await _repository.GetDownloadByIdAsync(downloadId, cancellationToken);
        if (job != null)
        {
            if (deleteFiles)
            {
                if (File.Exists(job.TargetPath))
                {
                    try { File.Delete(job.TargetPath); } catch { /* Ignore file lock */ }
                }
                if (File.Exists(job.TempPath))
                {
                    try { File.Delete(job.TempPath); } catch { /* Ignore file lock */ }
                }
            }
            await _repository.DeleteDownloadAsync(downloadId, cancellationToken);
        }
    }

    public Task<IReadOnlyList<DownloadJob>> GetAllDownloadsAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllDownloadsAsync(cancellationToken);
    }

    public async Task RedownloadAsync(Guid downloadId, CancellationToken cancellationToken = default)
    {
        if (_activeCancellations.TryRemove(downloadId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        var job = await _repository.GetDownloadByIdAsync(downloadId, cancellationToken);
        if (job == null) return;

        if (File.Exists(job.TargetPath))
        {
            try { File.Delete(job.TargetPath); } catch { /* Ignore file lock */ }
        }
        if (File.Exists(job.TempPath))
        {
            try { File.Delete(job.TempPath); } catch { /* Ignore file lock */ }
        }

        job.DownloadedBytes = 0;
        job.State = JobState.Created;
        job.FailureCode = null;
        job.FailureReason = null;
        job.FinishedAt = null;

        if (job.RangeSupported && job.ExpectedBytes is > 0)
        {
            job.Segments = SegmentPlanner.PlanSegments(job.Id, job.ExpectedBytes.Value, DefaultSegmentCount);
        }
        else
        {
            job.Segments.Clear();
        }

        await _repository.UpdateDownloadAsync(job, cancellationToken);
        JobStateChanged?.Invoke(job);

        await StartDownloadAsync(downloadId, cancellationToken);
    }

    public async Task UpdateJobAsync(DownloadJob job, CancellationToken cancellationToken = default)
    {
        await _repository.UpdateDownloadAsync(job, cancellationToken);
        JobStateChanged?.Invoke(job);
    }

    public async Task StopAllDownloadsAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Stopping all active downloads for clean application shutdown.");
        foreach (var kvp in _activeCancellations)
        {
            try
            {
                kvp.Value.Cancel();
            }
            catch { }
        }

        // Brief delay to allow background writers to finish flushing current chunks
        await Task.Delay(250, cancellationToken).ConfigureAwait(false);
    }
}

