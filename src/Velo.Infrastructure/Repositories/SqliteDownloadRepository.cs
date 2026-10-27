using System.Globalization;
using Microsoft.Data.Sqlite;
using Velo.Domain;

namespace Velo.Infrastructure.Repositories;

/// <summary>
/// SQLite persistence repository implementing crash recovery and transactional segment checkpoints (PRD Section 5.3 & 7.2).
/// </summary>
public sealed class SqliteDownloadRepository : IDownloadRepository
{
    private readonly string _connectionString;

    public SqliteDownloadRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task AddDownloadAsync(DownloadJob job, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO downloads (
                    id, original_url, effective_url, target_path, temp_path, display_name, host,
                    expected_bytes, downloaded_bytes, strong_etag, last_modified, range_supported,
                    state, priority, category, failure_code, failure_reason, created_at, updated_at, finished_at
                ) VALUES (
                    @id, @original_url, @effective_url, @target_path, @temp_path, @display_name, @host,
                    @expected_bytes, @downloaded_bytes, @strong_etag, @last_modified, @range_supported,
                    @state, @priority, @category, @failure_code, @failure_reason, @created_at, @updated_at, @finished_at
                );";

            cmd.Parameters.AddWithValue("@id", job.Id.ToString());
            cmd.Parameters.AddWithValue("@original_url", job.OriginalUrl);
            cmd.Parameters.AddWithValue("@effective_url", job.EffectiveUrl);
            cmd.Parameters.AddWithValue("@target_path", job.TargetPath);
            cmd.Parameters.AddWithValue("@temp_path", job.TempPath);
            cmd.Parameters.AddWithValue("@display_name", job.DisplayName);
            cmd.Parameters.AddWithValue("@host", job.Host);
            cmd.Parameters.AddWithValue("@expected_bytes", (object?)job.ExpectedBytes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@downloaded_bytes", job.DownloadedBytes);
            cmd.Parameters.AddWithValue("@strong_etag", (object?)job.StrongETag ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@last_modified", (object?)job.LastModified ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@range_supported", job.RangeSupported ? 1 : 0);
            cmd.Parameters.AddWithValue("@state", (int)job.State);
            cmd.Parameters.AddWithValue("@priority", (int)job.Priority);
            cmd.Parameters.AddWithValue("@category", (int)job.Category);
            cmd.Parameters.AddWithValue("@failure_code", (object?)job.FailureCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@failure_reason", (object?)job.FailureReason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created_at", job.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@updated_at", job.UpdatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@finished_at", (object?)job.FinishedAt?.ToString("O") ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var segment in job.Segments)
        {
            using var segCmd = connection.CreateCommand();
            segCmd.Transaction = transaction;
            segCmd.CommandText = @"
                INSERT INTO segments (
                    id, download_id, idx, start_byte, end_byte, next_byte, attempts, state, last_checkpoint_at
                ) VALUES (
                    @id, @download_id, @idx, @start_byte, @end_byte, @next_byte, @attempts, @state, @last_checkpoint_at
                );";

            segCmd.Parameters.AddWithValue("@id", segment.Id.ToString());
            segCmd.Parameters.AddWithValue("@download_id", job.Id.ToString());
            segCmd.Parameters.AddWithValue("@idx", segment.Index);
            segCmd.Parameters.AddWithValue("@start_byte", segment.StartByte);
            segCmd.Parameters.AddWithValue("@end_byte", segment.EndByte);
            segCmd.Parameters.AddWithValue("@next_byte", segment.NextByte);
            segCmd.Parameters.AddWithValue("@attempts", segment.Attempts);
            segCmd.Parameters.AddWithValue("@state", (int)segment.State);
            segCmd.Parameters.AddWithValue("@last_checkpoint_at", (object?)segment.LastCheckpointAt?.ToString("O") ?? DBNull.Value);

            await segCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateDownloadAsync(DownloadJob job, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                UPDATE downloads SET
                    original_url = @original_url,
                    effective_url = @effective_url,
                    display_name = @display_name,
                    host = @host,
                    target_path = @target_path,
                    temp_path = @temp_path,
                    expected_bytes = @expected_bytes,
                    downloaded_bytes = @downloaded_bytes,
                    strong_etag = @strong_etag,
                    last_modified = @last_modified,
                    range_supported = @range_supported,
                    state = @state,
                    priority = @priority,
                    category = @category,
                    failure_code = @failure_code,
                    failure_reason = @failure_reason,
                    updated_at = @updated_at,
                    finished_at = @finished_at
                WHERE id = @id;";

            cmd.Parameters.AddWithValue("@id", job.Id.ToString());
            cmd.Parameters.AddWithValue("@original_url", job.OriginalUrl);
            cmd.Parameters.AddWithValue("@effective_url", job.EffectiveUrl);
            cmd.Parameters.AddWithValue("@display_name", job.DisplayName);
            cmd.Parameters.AddWithValue("@host", job.Host);
            cmd.Parameters.AddWithValue("@target_path", job.TargetPath);
            cmd.Parameters.AddWithValue("@temp_path", job.TempPath);
            cmd.Parameters.AddWithValue("@expected_bytes", (object?)job.ExpectedBytes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@downloaded_bytes", job.DownloadedBytes);
            cmd.Parameters.AddWithValue("@strong_etag", (object?)job.StrongETag ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@last_modified", (object?)job.LastModified ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@range_supported", job.RangeSupported ? 1 : 0);
            cmd.Parameters.AddWithValue("@state", (int)job.State);
            cmd.Parameters.AddWithValue("@priority", (int)job.Priority);
            cmd.Parameters.AddWithValue("@category", (int)job.Category);
            cmd.Parameters.AddWithValue("@failure_code", (object?)job.FailureCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@failure_reason", (object?)job.FailureReason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@updated_at", DateTimeOffset.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("@finished_at", (object?)job.FinishedAt?.ToString("O") ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (job.Segments.Count > 0)
        {
            foreach (var segment in job.Segments)
            {
                using var segCmd = connection.CreateCommand();
                segCmd.Transaction = transaction;
                segCmd.CommandText = @"
                    INSERT INTO segments (
                        id, download_id, idx, start_byte, end_byte, next_byte, attempts, state, last_checkpoint_at
                    ) VALUES (
                        @id, @download_id, @idx, @start_byte, @end_byte, @next_byte, @attempts, @state, @last_checkpoint_at
                    )
                    ON CONFLICT(id) DO UPDATE SET
                        next_byte = excluded.next_byte,
                        attempts = excluded.attempts,
                        state = excluded.state,
                        last_checkpoint_at = excluded.last_checkpoint_at;";

                segCmd.Parameters.AddWithValue("@id", segment.Id.ToString());
                segCmd.Parameters.AddWithValue("@download_id", job.Id.ToString());
                segCmd.Parameters.AddWithValue("@idx", segment.Index);
                segCmd.Parameters.AddWithValue("@start_byte", segment.StartByte);
                segCmd.Parameters.AddWithValue("@end_byte", segment.EndByte);
                segCmd.Parameters.AddWithValue("@next_byte", segment.NextByte);
                segCmd.Parameters.AddWithValue("@attempts", segment.Attempts);
                segCmd.Parameters.AddWithValue("@state", (int)segment.State);
                segCmd.Parameters.AddWithValue("@last_checkpoint_at", (object?)segment.LastCheckpointAt?.ToString("O") ?? DBNull.Value);

                await segCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CheckpointSegmentAsync(DownloadSegment segment, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE segments SET
                next_byte = @next_byte,
                attempts = @attempts,
                state = @state,
                last_checkpoint_at = @last_checkpoint_at
            WHERE id = @id;";

        cmd.Parameters.AddWithValue("@id", segment.Id.ToString());
        cmd.Parameters.AddWithValue("@next_byte", segment.NextByte);
        cmd.Parameters.AddWithValue("@attempts", segment.Attempts);
        cmd.Parameters.AddWithValue("@state", (int)segment.State);
        cmd.Parameters.AddWithValue("@last_checkpoint_at", (object?)segment.LastCheckpointAt?.ToString("O") ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DownloadJob?> GetDownloadByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        DownloadJob? job = null;

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM downloads WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id.ToString());

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                job = MapDownloadJob(reader);
            }
        }

        if (job == null) return null;

        using (var segCmd = connection.CreateCommand())
        {
            segCmd.CommandText = "SELECT * FROM segments WHERE download_id = @download_id ORDER BY idx ASC;";
            segCmd.Parameters.AddWithValue("@download_id", id.ToString());

            using var reader = await segCmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                job.Segments.Add(MapDownloadSegment(reader));
            }
        }

        return job;
    }

    public async Task<IReadOnlyList<DownloadJob>> GetAllDownloadsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var list = new List<DownloadJob>();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM downloads ORDER BY created_at DESC;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapDownloadJob(reader));
            }
        }

        if (list.Count > 0)
        {
            using var segCmd = connection.CreateCommand();
            segCmd.CommandText = "SELECT * FROM segments ORDER BY download_id, idx ASC;";
            using var segReader = await segCmd.ExecuteReaderAsync(cancellationToken);
            var lookup = list.ToDictionary(d => d.Id);
            while (await segReader.ReadAsync(cancellationToken))
            {
                var segment = MapDownloadSegment(segReader);
                if (lookup.TryGetValue(segment.DownloadId, out var job))
                {
                    job.Segments.Add(segment);
                }
            }
        }

        return list;
    }

    public async Task DeleteDownloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM downloads WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id.ToString());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ReconcileInterruptedDownloadsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        // Transition any job that was unexpectedly left in Downloading or Probing to Interrupted
        cmd.CommandText = @"
            UPDATE downloads 
            SET state = @interruptedState, updated_at = @now
            WHERE state IN (@probingState, @downloadingState);";

        cmd.Parameters.AddWithValue("@interruptedState", (int)JobState.Interrupted);
        cmd.Parameters.AddWithValue("@probingState", (int)JobState.Probing);
        cmd.Parameters.AddWithValue("@downloadingState", (int)JobState.Downloading);
        cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DownloadJob MapDownloadJob(SqliteDataReader r)
    {
        return new DownloadJob
        {
            Id = Guid.Parse(r.GetString(r.GetOrdinal("id"))),
            OriginalUrl = r.GetString(r.GetOrdinal("original_url")),
            EffectiveUrl = r.GetString(r.GetOrdinal("effective_url")),
            TargetPath = r.GetString(r.GetOrdinal("target_path")),
            TempPath = r.GetString(r.GetOrdinal("temp_path")),
            DisplayName = r.GetString(r.GetOrdinal("display_name")),
            Host = r.GetString(r.GetOrdinal("host")),
            ExpectedBytes = r.IsDBNull(r.GetOrdinal("expected_bytes")) ? null : r.GetInt64(r.GetOrdinal("expected_bytes")),
            DownloadedBytes = r.GetInt64(r.GetOrdinal("downloaded_bytes")),
            StrongETag = r.IsDBNull(r.GetOrdinal("strong_etag")) ? null : r.GetString(r.GetOrdinal("strong_etag")),
            LastModified = r.IsDBNull(r.GetOrdinal("last_modified")) ? null : r.GetString(r.GetOrdinal("last_modified")),
            RangeSupported = r.GetInt32(r.GetOrdinal("range_supported")) == 1,
            State = (JobState)r.GetInt32(r.GetOrdinal("state")),
            Priority = (JobPriority)r.GetInt32(r.GetOrdinal("priority")),
            Category = (CategoryType)(r.IsDBNull(r.GetOrdinal("category")) ? 0 : r.GetInt32(r.GetOrdinal("category"))),
            FailureCode = r.IsDBNull(r.GetOrdinal("failure_code")) ? null : r.GetString(r.GetOrdinal("failure_code")),
            FailureReason = r.IsDBNull(r.GetOrdinal("failure_reason")) ? null : r.GetString(r.GetOrdinal("failure_reason")),
            CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("created_at")), CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("updated_at")), CultureInfo.InvariantCulture),
            FinishedAt = r.IsDBNull(r.GetOrdinal("finished_at")) ? null : DateTimeOffset.Parse(r.GetString(r.GetOrdinal("finished_at")), CultureInfo.InvariantCulture)
        };
    }

    private static DownloadSegment MapDownloadSegment(SqliteDataReader r)
    {
        return new DownloadSegment
        {
            Id = Guid.Parse(r.GetString(r.GetOrdinal("id"))),
            DownloadId = Guid.Parse(r.GetString(r.GetOrdinal("download_id"))),
            Index = r.GetInt32(r.GetOrdinal("idx")),
            StartByte = r.GetInt64(r.GetOrdinal("start_byte")),
            EndByte = r.GetInt64(r.GetOrdinal("end_byte")),
            NextByte = r.GetInt64(r.GetOrdinal("next_byte")),
            Attempts = r.GetInt32(r.GetOrdinal("attempts")),
            State = (SegmentState)r.GetInt32(r.GetOrdinal("state")),
            LastCheckpointAt = r.IsDBNull(r.GetOrdinal("last_checkpoint_at")) ? null : DateTimeOffset.Parse(r.GetString(r.GetOrdinal("last_checkpoint_at")), CultureInfo.InvariantCulture)
        };
    }
}
