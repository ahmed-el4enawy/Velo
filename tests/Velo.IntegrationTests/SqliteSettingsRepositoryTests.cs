using FluentAssertions;
using Microsoft.Data.Sqlite;
using Velo.Domain;
using Velo.Infrastructure.Database;
using Velo.Infrastructure.Repositories;
using Xunit;

namespace Velo.IntegrationTests;

public class SqliteSettingsRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;
    private readonly SqliteSettingsRepository _repository;

    public SqliteSettingsRepositoryTests()
    {
        _connectionString = $"Data Source=TestSettings_{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        SqliteDatabaseInitializer.Initialize(_connectionString);
        _repository = new SqliteSettingsRepository(_connectionString);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task GetSettingsAsync_DefaultState_ReturnsSafeDefaults()
    {
        // Act
        var settings = await _repository.GetSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.MaxConcurrentDownloads.Should().BeInRange(1, 10);
        settings.SegmentsPerDownload.Should().BeInRange(1, 16);
        settings.MaxConnectionsPerHost.Should().BeInRange(1, 32);
        settings.DefaultDownloadDirectory.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsAndReadsBackCorrectly()
    {
        // Arrange
        var custom = new AppSettings
        {
            MaxConcurrentDownloads = 6,
            SegmentsPerDownload = 12,
            MaxConnectionsPerHost = 18,
            TransientFailureRetries = 5,
            SpeedLimitBytesPerSecond = 5 * 1024 * 1024,
            ClipboardMonitoringEnabled = true,
            DefaultDownloadDirectory = "C:\\Downloads\\Velo"
        };

        // Act
        await _repository.SaveSettingsAsync(custom);
        var loaded = await _repository.GetSettingsAsync();

        // Assert
        loaded.MaxConcurrentDownloads.Should().Be(6);
        loaded.SegmentsPerDownload.Should().Be(12);
        loaded.MaxConnectionsPerHost.Should().Be(18);
        loaded.TransientFailureRetries.Should().Be(5);
        loaded.SpeedLimitBytesPerSecond.Should().Be(5 * 1024 * 1024);
        loaded.ClipboardMonitoringEnabled.Should().BeTrue();
        loaded.DefaultDownloadDirectory.Should().Be("C:\\Downloads\\Velo");
    }

    [Fact]
    public async Task SetRawValueAsync_UpsertsKeySuccessfully()
    {
        // Act
        await _repository.SetRawValueAsync("custom.test_key", "custom_test_value");
        var value = await _repository.GetRawValueAsync("custom.test_key");

        // Assert
        value.Should().Be("custom_test_value");
    }
}
