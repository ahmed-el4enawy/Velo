using FluentAssertions;
using Velo.Domain;
using Velo.Infrastructure.Database;
using Velo.Infrastructure.Repositories;
using Xunit;

namespace Velo.UnitTests;

public sealed class SqliteSettingsRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SqliteSettingsRepository _repository;

    public SqliteSettingsRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"velo_test_settings_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath};";
        SqliteDatabaseInitializer.Initialize(_connectionString);
        _repository = new SqliteSettingsRepository(_connectionString);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* Ignore */ }
        }
    }

    [Fact]
    public async Task GetSettingsAsync_DefaultState_ShouldReturnSensibleDefaults()
    {
        var settings = await _repository.GetSettingsAsync();

        settings.MaxConcurrentDownloads.Should().Be(3);
        settings.SegmentsPerDownload.Should().Be(4);
        settings.MaxConnectionsPerHost.Should().Be(8);
        settings.TransientFailureRetries.Should().Be(5);
        settings.SpeedLimitBytesPerSecond.Should().BeNull();
        settings.ClipboardMonitoringEnabled.Should().BeFalse();
        settings.DefaultDownloadDirectory.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SaveSettingsAsync_CustomValues_ShouldPersistAndReloadAccurately()
    {
        var settings = new AppSettings
        {
            MaxConcurrentDownloads = 6,
            SegmentsPerDownload = 8,
            MaxConnectionsPerHost = 12,
            TransientFailureRetries = 10,
            SpeedLimitBytesPerSecond = 5 * 1024 * 1024, // 5 MB/s
            ClipboardMonitoringEnabled = true,
            DefaultDownloadDirectory = @"C:\Downloads\Velo"
        };

        await _repository.SaveSettingsAsync(settings);
        var loaded = await _repository.GetSettingsAsync();

        loaded.MaxConcurrentDownloads.Should().Be(6);
        loaded.SegmentsPerDownload.Should().Be(8);
        loaded.MaxConnectionsPerHost.Should().Be(12);
        loaded.TransientFailureRetries.Should().Be(10);
        loaded.SpeedLimitBytesPerSecond.Should().Be(5 * 1024 * 1024);
        loaded.ClipboardMonitoringEnabled.Should().BeTrue();
        loaded.DefaultDownloadDirectory.Should().Be(@"C:\Downloads\Velo");
    }

    [Fact]
    public async Task GetSettingsAsync_OutOfBoundsValues_ShouldBeClampedSafely()
    {
        await _repository.SetRawValueAsync(SqliteSettingsRepository.KeyMaxConcurrentDownloads, "999");
        await _repository.SetRawValueAsync(SqliteSettingsRepository.KeySegmentsPerDownload, "0");

        var loaded = await _repository.GetSettingsAsync();

        loaded.MaxConcurrentDownloads.Should().Be(10); // Clamped to 10
        loaded.SegmentsPerDownload.Should().Be(1);     // Clamped to 1
    }
}
