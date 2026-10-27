using Velo.Application;
using Velo.Domain;

namespace Velo.EndToEndTests;

public class FakeSettingsRepository : ISettingsRepository
{
    public AppSettings CurrentSettings { get; set; } = new();
    public bool SaveCalled { get; private set; }

    public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CurrentSettings);
    }

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        SaveCalled = true;
        CurrentSettings = settings;
        return Task.CompletedTask;
    }

    public Task<string?> GetRawValueAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    public Task SetRawValueAsync(string key, string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class FakeDownloadService : IDownloadService
{
#pragma warning disable CS0067
    public event Action<Guid, long, double>? DownloadProgressUpdated;
#pragma warning restore CS0067
    public event Action<DownloadJob>? JobStateChanged;

    public List<DownloadJob> Jobs { get; } = [];

    public Task<DownloadJob> CreateDownloadAsync(string url, string? customFileName = null, string? destinationFolder = null, CancellationToken cancellationToken = default)
    {
        var job = new DownloadJob { OriginalUrl = url, DisplayName = customFileName ?? "download.bin" };
        Jobs.Add(job);
        JobStateChanged?.Invoke(job);
        return Task.FromResult(job);
    }

    public Task StartDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PauseDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ResumeDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CancelDownloadAsync(Guid downloadId, bool deletePartialFiles = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteDownloadAsync(Guid downloadId, bool deleteFiles = false, CancellationToken cancellationToken = default)
    {
        Jobs.RemoveAll(j => j.Id == downloadId);
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<DownloadJob>> GetAllDownloadsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DownloadJob>>(Jobs);
    public Task RedownloadAsync(Guid downloadId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateJobAsync(DownloadJob job, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
