namespace Velo.Domain;

/// <summary>
/// Structured diagnostic logger with automated secret redaction according to PRD v2.0 Section 8 & FR-14.
/// </summary>
public interface IDiagnosticLogger
{
    Task LogEventAsync(string level, string message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DiagnosticEvent>> GetRecentEventsAsync(int maxCount = 100, CancellationToken cancellationToken = default);
    string RedactSensitiveData(string rawText);
}

public sealed record DiagnosticEvent(long Id, string Level, string Message, DateTimeOffset Timestamp);
