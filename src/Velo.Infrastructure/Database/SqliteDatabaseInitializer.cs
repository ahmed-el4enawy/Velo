using Microsoft.Data.Sqlite;

namespace Velo.Infrastructure.Database;

/// <summary>
/// Initializes SQLite database schema according to PRD v2.0 Section 7.2.
/// </summary>
public static class SqliteDatabaseInitializer
{
    public static void Initialize(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;

            CREATE TABLE IF NOT EXISTS downloads (
                id TEXT PRIMARY KEY,
                original_url TEXT NOT NULL,
                effective_url TEXT NOT NULL,
                target_path TEXT NOT NULL,
                temp_path TEXT NOT NULL,
                display_name TEXT NOT NULL,
                host TEXT NOT NULL,
                expected_bytes INTEGER,
                downloaded_bytes INTEGER NOT NULL DEFAULT 0,
                strong_etag TEXT,
                last_modified TEXT,
                range_supported INTEGER NOT NULL DEFAULT 0,
                state INTEGER NOT NULL DEFAULT 0,
                priority INTEGER NOT NULL DEFAULT 1,
                category INTEGER NOT NULL DEFAULT 0,
                failure_code TEXT,
                failure_reason TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                finished_at TEXT
            );

            CREATE TABLE IF NOT EXISTS segments (
                id TEXT PRIMARY KEY,
                download_id TEXT NOT NULL,
                idx INTEGER NOT NULL,
                start_byte INTEGER NOT NULL,
                end_byte INTEGER NOT NULL,
                next_byte INTEGER NOT NULL,
                attempts INTEGER NOT NULL DEFAULT 0,
                state INTEGER NOT NULL DEFAULT 0,
                last_checkpoint_at TEXT,
                FOREIGN KEY (download_id) REFERENCES downloads(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS diagnostic_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                level TEXT NOT NULL,
                message TEXT NOT NULL,
                timestamp TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_segments_download_id ON segments(download_id);
            CREATE INDEX IF NOT EXISTS idx_downloads_state ON downloads(state);
        ";
        command.ExecuteNonQuery();

        // Safe idempotent migration: Check if 'category' column exists in 'downloads' table
        using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA table_info(downloads);";
        bool hasCategory = false;
        using (var reader = pragmaCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "category", StringComparison.OrdinalIgnoreCase))
                {
                    hasCategory = true;
                    break;
                }
            }
        }

        if (!hasCategory)
        {
            using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE downloads ADD COLUMN category INTEGER NOT NULL DEFAULT 0;";
            alterCmd.ExecuteNonQuery();
        }
    }
}
