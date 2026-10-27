using System.Globalization;
using Microsoft.Data.Sqlite;
using Velo.Domain;

namespace Velo.Infrastructure.Repositories;

/// <summary>
/// SQLite-backed persistent settings repository storing keyed user preferences (PRD Section 7.2).
/// </summary>
public sealed class SqliteSettingsRepository : ISettingsRepository
{
    private readonly string _connectionString;

    public const string KeyMaxConcurrentDownloads = "queue.max_concurrent_downloads";
    public const string KeySegmentsPerDownload = "engine.segments_per_download";
    public const string KeyMaxConnectionsPerHost = "engine.max_connections_per_host";
    public const string KeyTransientFailureRetries = "engine.transient_failure_retries";
    public const string KeySpeedLimitBytes = "engine.speed_limit_bytes_per_sec";
    public const string KeyClipboardMonitoring = "app.clipboard_monitoring_enabled";
    public const string KeyDefaultDownloadDirectory = "app.default_download_directory";

    public SqliteSettingsRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = new AppSettings();

        // Default download directory fallback if not persisted
        settings.DefaultDownloadDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings;";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string key = reader.GetString(0);
            string value = reader.GetString(1);

            switch (key)
            {
                case KeyMaxConcurrentDownloads when int.TryParse(value, CultureInfo.InvariantCulture, out int maxConcurrent):
                    settings.MaxConcurrentDownloads = Math.Clamp(maxConcurrent, 1, 10);
                    break;
                case KeySegmentsPerDownload when int.TryParse(value, CultureInfo.InvariantCulture, out int segments):
                    settings.SegmentsPerDownload = Math.Clamp(segments, 1, 16);
                    break;
                case KeyMaxConnectionsPerHost when int.TryParse(value, CultureInfo.InvariantCulture, out int conns):
                    settings.MaxConnectionsPerHost = Math.Clamp(conns, 1, 32);
                    break;
                case KeyTransientFailureRetries when int.TryParse(value, CultureInfo.InvariantCulture, out int retries):
                    settings.TransientFailureRetries = Math.Clamp(retries, 0, 20);
                    break;
                case KeySpeedLimitBytes:
                    if (long.TryParse(value, CultureInfo.InvariantCulture, out long speedLimit) && speedLimit > 0)
                    {
                        settings.SpeedLimitBytesPerSecond = speedLimit;
                    }
                    else
                    {
                        settings.SpeedLimitBytesPerSecond = null;
                    }
                    break;
                case KeyClipboardMonitoring when bool.TryParse(value, out bool clipboard):
                    settings.ClipboardMonitoringEnabled = clipboard;
                    break;
                case KeyDefaultDownloadDirectory when !string.IsNullOrWhiteSpace(value):
                    settings.DefaultDownloadDirectory = value;
                    break;
            }
        }

        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await UpsertValueInternalAsync(connection, transaction, KeyMaxConcurrentDownloads, settings.MaxConcurrentDownloads.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await UpsertValueInternalAsync(connection, transaction, KeySegmentsPerDownload, settings.SegmentsPerDownload.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await UpsertValueInternalAsync(connection, transaction, KeyMaxConnectionsPerHost, settings.MaxConnectionsPerHost.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await UpsertValueInternalAsync(connection, transaction, KeyTransientFailureRetries, settings.TransientFailureRetries.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await UpsertValueInternalAsync(connection, transaction, KeySpeedLimitBytes, (settings.SpeedLimitBytesPerSecond?.ToString(CultureInfo.InvariantCulture)) ?? "0", cancellationToken);
        await UpsertValueInternalAsync(connection, transaction, KeyClipboardMonitoring, settings.ClipboardMonitoringEnabled.ToString(), cancellationToken);
        await UpsertValueInternalAsync(connection, transaction, KeyDefaultDownloadDirectory, settings.DefaultDownloadDirectory, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<string?> GetRawValueAsync(string key, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = @key;";
        cmd.Parameters.AddWithValue("@key", key);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    public async Task SetRawValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await UpsertValueInternalAsync(connection, null, key, value, cancellationToken);
    }

    private static async Task UpsertValueInternalAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            INSERT INTO settings (key, value, updated_at)
            VALUES (@key, @value, @updated_at)
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                updated_at = excluded.updated_at;";

        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.Parameters.AddWithValue("@updated_at", DateTimeOffset.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
