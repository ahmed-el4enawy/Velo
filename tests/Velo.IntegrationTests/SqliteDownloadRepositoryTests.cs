using FluentAssertions;
using Microsoft.Data.Sqlite;
using Velo.Domain;
using Velo.Infrastructure.Database;
using Velo.Infrastructure.Repositories;
using Xunit;

namespace Velo.IntegrationTests;

public class SqliteDownloadRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;
    private readonly SqliteDownloadRepository _repository;

    public SqliteDownloadRepositoryTests()
    {
        // Shared in-memory SQLite database
        _connectionString = $"Data Source=Test_{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        SqliteDatabaseInitializer.Initialize(_connectionString);
        _repository = new SqliteDownloadRepository(_connectionString);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task AddAndGetDownloadAsync_ShouldPersistJobAndSegments()
    {
        var job = new DownloadJob
        {
            OriginalUrl = "https://example.com/archive.zip",
            EffectiveUrl = "https://example.com/archive.zip",
            TargetPath = "C:\\Downloads\\archive.zip",
            TempPath = "C:\\Downloads\\archive.zip.velo.part",
            DisplayName = "archive.zip",
            Host = "example.com",
            ExpectedBytes = 1000000,
            DownloadedBytes = 250000,
            RangeSupported = true,
            StrongETag = "\"etag-999\"",
            State = JobState.Downloading
        };

        var seg1 = new DownloadSegment
        {
            DownloadId = job.Id,
            Index = 0,
            StartByte = 0,
            EndByte = 499999,
            NextByte = 250000,
            State = SegmentState.Downloading
        };

        var seg2 = new DownloadSegment
        {
            DownloadId = job.Id,
            Index = 1,
            StartByte = 500000,
            EndByte = 999999,
            NextByte = 500000,
            State = SegmentState.Pending
        };

        job.Segments.Add(seg1);
        job.Segments.Add(seg2);

        await _repository.AddDownloadAsync(job);

        var retrieved = await _repository.GetDownloadByIdAsync(job.Id);

        retrieved.Should().NotBeNull();
        retrieved!.DisplayName.Should().Be("archive.zip");
        retrieved.ExpectedBytes.Should().Be(1000000);
        retrieved.Segments.Should().HaveCount(2);
        retrieved.Segments[0].NextByte.Should().Be(250000);
        retrieved.Segments[1].NextByte.Should().Be(500000);
    }

    [Fact]
    public async Task CheckpointSegmentAsync_ShouldUpdateNextByte()
    {
        var job = new DownloadJob
        {
            OriginalUrl = "https://example.com/file.bin",
            EffectiveUrl = "https://example.com/file.bin",
            TargetPath = "C:\\Downloads\\file.bin",
            TempPath = "C:\\Downloads\\file.bin.velo.part",
            DisplayName = "file.bin",
            Host = "example.com"
        };

        var seg = new DownloadSegment
        {
            DownloadId = job.Id,
            Index = 0,
            StartByte = 0,
            EndByte = 1000,
            NextByte = 0
        };
        job.Segments.Add(seg);

        await _repository.AddDownloadAsync(job);

        // Update checkpoint
        seg.NextByte = 500;
        seg.LastCheckpointAt = DateTimeOffset.UtcNow;
        await _repository.CheckpointSegmentAsync(seg);

        var retrieved = await _repository.GetDownloadByIdAsync(job.Id);
        retrieved!.Segments[0].NextByte.Should().Be(500);
    }

    [Fact]
    public async Task ReconcileInterruptedDownloadsAsync_ShouldMarkActiveJobsAsInterrupted()
    {
        var job = new DownloadJob
        {
            OriginalUrl = "https://example.com/movie.mp4",
            EffectiveUrl = "https://example.com/movie.mp4",
            TargetPath = "C:\\Downloads\\movie.mp4",
            TempPath = "C:\\Downloads\\movie.mp4.velo.part",
            DisplayName = "movie.mp4",
            Host = "example.com",
            State = JobState.Downloading
        };

        await _repository.AddDownloadAsync(job);

        // Simulate crash recovery on boot
        await _repository.ReconcileInterruptedDownloadsAsync();

        var retrieved = await _repository.GetDownloadByIdAsync(job.Id);
        retrieved!.State.Should().Be(JobState.Interrupted, "Active downloads from unexpected shutdown must transition to Interrupted");
    }

    [Fact]
    public async Task UpdateDownloadAsync_ShouldPersistSegmentProgressAndState()
    {
        var job = new DownloadJob
        {
            OriginalUrl = "https://example.com/dataset.tar",
            EffectiveUrl = "https://example.com/dataset.tar",
            TargetPath = "C:\\Downloads\\dataset.tar",
            TempPath = "C:\\Downloads\\dataset.tar.velo.part",
            DisplayName = "dataset.tar",
            Host = "example.com",
            ExpectedBytes = 2000,
            State = JobState.Downloading
        };

        var seg = new DownloadSegment
        {
            DownloadId = job.Id,
            Index = 0,
            StartByte = 0,
            EndByte = 1999,
            NextByte = 1000,
            State = SegmentState.Downloading
        };
        job.Segments.Add(seg);
        await _repository.AddDownloadAsync(job);

        // Advance segment and complete it
        seg.NextByte = 2000;
        seg.State = SegmentState.Completed;
        job.State = JobState.Completed;
        job.DownloadedBytes = 2000;

        await _repository.UpdateDownloadAsync(job);

        var retrieved = await _repository.GetDownloadByIdAsync(job.Id);
        retrieved!.Segments.Should().HaveCount(1);
        retrieved.Segments[0].NextByte.Should().Be(2000);
        retrieved.Segments[0].IsCompleted.Should().BeTrue();

        var all = await _repository.GetAllDownloadsAsync();
        all.First(d => d.Id == job.Id).Segments.Should().HaveCount(1);
        all.First(d => d.Id == job.Id).Segments[0].IsCompleted.Should().BeTrue();
    }
}
