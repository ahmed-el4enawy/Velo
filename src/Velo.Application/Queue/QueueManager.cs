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
            // Prevent duplicate entries in active or queued lists
            if (_activeJobs.ContainsKey(downloadId) || _queuedJobs.Contains(downloadId))
            {
                _logger?.LogDebug("Queue: Download {Id} is already in the queue or active. Skipping duplicate enqueue.", downloadId);
                return;
            }

            var job = _repository.GetDownloadByIdAsync(downloadId, cancellationToken).GetAwaiter().GetResult();
            string host = job?.Host ?? string.Empty;

            int activeCount = _activeJobs.Count;
            int activeHostCount = string.IsNullOrEmpty(host) ? 0 : GetActiveCountForHost(host);

            if (activeCount < MaxConcurrentDownloads && (string.IsNullOrEmpty(host) || activeHostCount < MaxConnectionsPerHost))
            {
                _activeJobs.TryAdd(downloadId, true);
                canStartImmediately = true;
            }
            else
            {
                _queuedJobs.Add(downloadId);
            }
        }

        if (canStartImmediately)
        {
            _logger?.LogInformation("Queue: Starting download {Id} immediately.", downloadId);
            await _downloadService.StartDownloadAsync(downloadId, cancellationToken);
        }
        else
        {
            _logger?.LogInformation("Queue: Concurrency cap reached. Enqueueing {Id}.", downloadId);
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
                // Find next queued job that complies with per-host connection limits
                for (int i = 0; i < _queuedJobs.Count; i++)
                {
                    var candidateId = _queuedJobs[i];
                    var candidateJob = _repository.GetDownloadByIdAsync(candidateId, cancellationToken).GetAwaiter().GetResult();
                    string candidateHost = candidateJob?.Host ?? string.Empty;

                    if (string.IsNullOrEmpty(candidateHost) || GetActiveCountForHost(candidateHost) < MaxConnectionsPerHost)
                    {
                        nextJobToStart = candidateId;
                        _queuedJobs.RemoveAt(i);
                        _activeJobs.TryAdd(candidateId, true);
                        break;
                    }
                }
            }
        }

        if (nextJobToStart.HasValue)
        {
            _logger?.LogInformation("Queue: Slot available. Promoting queued download {Id}.", nextJobToStart.Value);
            await _downloadService.StartDownloadAsync(nextJobToStart.Value, cancellationToken);
        }
    }

    private int GetActiveCountForHost(string host)
    {
        int count = 0;
        foreach (var activeId in _activeJobs.Keys)
        {
            var activeJob = _repository.GetDownloadByIdAsync(activeId, CancellationToken.None).GetAwaiter().GetResult();
            if (activeJob != null && string.Equals(activeJob.Host, host, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }
        return count;
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
