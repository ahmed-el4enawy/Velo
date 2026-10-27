using System.Collections.Concurrent;
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

    public DownloadService(
        IHttpProbe probe,
        IDownloadEngine engine,
        IDownloadRepository repository,
        ILogger<DownloadService>? logger = null)
    {
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

        string defaultFolder = !string.IsNullOrWhiteSpace(destinationFolder)
            ? destinationFolder
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        string fileName = !string.IsNullOrWhiteSpace(customFileName)
            ? SafePathValidator.SanitizeFileName(customFileName)
            : probeResult.ContentDispositionFileName ?? "download.bin";

        string targetPath = Path.Combine(defaultFolder, fileName);
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
            State = JobState.Created
        };

        if (job.RangeSupported && job.ExpectedBytes is > 0)
        {
            job.Segments = SegmentPlanner.PlanSegments(job.Id, job.ExpectedBytes.Value, 4);
        }

        await _repository.AddDownloadAsync(job, cancellationToken);
        return job;
    }

    public async Task StartDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetDownloadByIdAsync(downloadId, cancellationToken);
        if (job == null)
        {
            throw new KeyNotFoundException($"Download job with ID {downloadId} was not found.");
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
            try
            {
                var progress = new Progress<long>(bytes =>
                {
                    job.DownloadedBytes += bytes;
                });

                await _engine.ExecuteDownloadAsync(job, progress, cts.Token);
                await _repository.UpdateDownloadAsync(job, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                job.State = JobState.Paused;
                await _repository.UpdateDownloadAsync(job, CancellationToken.None);
            }
            catch (Exception ex)
            {
                job.State = JobState.Failed;
                job.FailureCode = ex.GetType().Name;
                job.FailureReason = ex.Message;
                await _repository.UpdateDownloadAsync(job, CancellationToken.None);
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
        if (_activeCancellations.TryRemove(downloadId, out var cts))
        {
            cts.Cancel();
        }

        var job = await _repository.GetDownloadByIdAsync(downloadId, cancellationToken);
        if (job != null)
        {
            job.State = JobState.Paused;
            await _repository.UpdateDownloadAsync(job, cancellationToken);
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

            if (deletePartialFiles && File.Exists(job.TempPath))
            {
                try { File.Delete(job.TempPath); } catch { /* Ignore file lock */ }
            }
        }
    }

    public Task<IReadOnlyList<DownloadJob>> GetAllDownloadsAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllDownloadsAsync(cancellationToken);
    }
}
