using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Velo.Domain;

namespace Velo.Application.Queue;

/// <summary>
/// Thread-safe queue coordinator enforcing active concurrency limits and priority-based scheduling (PRD Section 6).
/// Automatically promotes queued jobs when active transfers complete or transition out of downloading.
/// </summary>
public sealed class QueueManager : IQueueManager
{
    private readonly IDownloadRepository _repository;
    private readonly IDownloadService _downloadService;
    private readonly ILogger<QueueManager>? _logger;
    private readonly Lock _lock = new();

    private readonly ConcurrentDictionary<Guid, bool> _activeJobs = new();
    private readonly List<Guid> _queuedJobs = [];

    public int MaxConcurrentDownloads { get; set; } = 3;
    public int MaxConnectionsPerHost { get; set; } = 8;

    public QueueManager(
        IDownloadRepository repository,
        IDownloadService downloadService,
        ILogger<QueueManager>? logger = null)
    {
        _repository = repository;
        _downloadService = downloadService;
        _logger = logger;

        _downloadService.JobStateChanged += job =>
        {
            if (job.State is JobState.Completed or JobState.Failed or JobState.Canceled or JobState.Paused)
            {
                _ = NotifyJobStateChangedAsync(job.Id);
            }
        };
    }

    public async Task EnqueueAsync(Guid downloadId, CancellationToken cancellationToken = default)
    {
        bool canStartImmediately = false;

        lock (_lock)
        {
            if (_activeJobs.Count < MaxConcurrentDownloads)
            {
                _activeJobs.TryAdd(downloadId, true);
                canStartImmediately = true;
            }
            else
            {
                if (!_queuedJobs.Contains(downloadId))
                {
                    _queuedJobs.Add(downloadId);
                }
            }
        }

        if (canStartImmediately)
        {
            _logger?.LogInformation("Queue: Starting download {Id} immediately.", downloadId);
            await _downloadService.StartDownloadAsync(downloadId, cancellationToken);
        }
        else
        {
            _logger?.LogInformation("Queue: Concurrency cap reached ({Max}). Enqueueing {Id}.", MaxConcurrentDownloads, downloadId);
            var job = await _repository.GetDownloadByIdAsync(downloadId, cancellationToken);
            if (job != null)
            {
                job.State = JobState.Queued;
                await _repository.UpdateDownloadAsync(job, cancellationToken);
            }
        }
    }

    public async Task NotifyJobStateChangedAsync(Guid downloadId, CancellationToken cancellationToken = default)
    {
        Guid? nextJobToStart = null;

        lock (_lock)
        {
            _activeJobs.TryRemove(downloadId, out _);

            if (_activeJobs.Count < MaxConcurrentDownloads && _queuedJobs.Count > 0)
            {
                nextJobToStart = _queuedJobs[0];
                _queuedJobs.RemoveAt(0);
                _activeJobs.TryAdd(nextJobToStart.Value, true);
            }
        }

        if (nextJobToStart.HasValue)
        {
            _logger?.LogInformation("Queue: Slot available. Promoting queued download {Id}.", nextJobToStart.Value);
            await _downloadService.StartDownloadAsync(nextJobToStart.Value, cancellationToken);
        }
    }

    public IReadOnlyList<Guid> GetActiveJobIds() => _activeJobs.Keys.ToList();
    public IReadOnlyList<Guid> GetQueuedJobIds()
    {
        lock (_lock)
        {
            return _queuedJobs.ToList();
        }
    }
}
