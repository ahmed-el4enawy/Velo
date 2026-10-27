using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Velo.Domain;

namespace Velo.Infrastructure.Logging;

/// <summary>
/// SQLite diagnostic event logger with automatic token and secret redaction (PRD FR-14 & Section 8.4).
/// </summary>
public sealed partial class SqliteDiagnosticLogger : IDiagnosticLogger
{
    private readonly string _connectionString;

    [GeneratedRegex(@"(?i)(token|key|secret|auth|signature|sig|password|api_key)=([^&\s]+)", RegexOptions.Compiled)]
    private static partial Regex SensitiveQueryParamRegex();

    [GeneratedRegex(@"(?i)(bearer\s+)[a-zA-Z0-9_\-\.]+", RegexOptions.Compiled)]
    private static partial Regex BearerTokenRegex();

    public SqliteDiagnosticLogger(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task LogEventAsync(string level, string message, CancellationToken cancellationToken = default)
    {
        string sanitized = RedactSensitiveData(message);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO diagnostic_events (level, message, timestamp)
            VALUES (@level, @message, @timestamp);

            -- Automatically prune oldest records past 1,000 limit
            DELETE FROM diagnostic_events 
            WHERE id NOT IN (
                SELECT id FROM diagnostic_events ORDER BY id DESC LIMIT 1000
            );";

        cmd.Parameters.AddWithValue("@level", level);
        cmd.Parameters.AddWithValue("@message", sanitized);
        cmd.Parameters.AddWithValue("@timestamp", DateTimeOffset.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DiagnosticEvent>> GetRecentEventsAsync(int maxCount = 100, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var list = new List<DiagnosticEvent>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, level, message, timestamp FROM diagnostic_events ORDER BY id DESC LIMIT @limit;";
        cmd.Parameters.AddWithValue("@limit", Math.Clamp(maxCount, 1, 1000));

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new DiagnosticEvent(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3))));
        }

        return list;
    }

    public string RedactSensitiveData(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

        string redacted = SensitiveQueryParamRegex().Replace(rawText, "$1=[REDACTED]");
        redacted = BearerTokenRegex().Replace(redacted, "$1[REDACTED]");
        return redacted;
    }
}
