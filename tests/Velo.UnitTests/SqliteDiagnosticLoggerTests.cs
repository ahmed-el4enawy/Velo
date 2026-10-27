using FluentAssertions;
using Microsoft.Data.Sqlite;
using Velo.Infrastructure.Database;
using Velo.Infrastructure.Logging;
using Xunit;

namespace Velo.UnitTests;

public class SqliteDiagnosticLoggerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;
    private readonly SqliteDiagnosticLogger _logger;

    public SqliteDiagnosticLoggerTests()
    {
        _connectionString = $"Data Source=DiagTest_{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        SqliteDatabaseInitializer.Initialize(_connectionString);
        _logger = new SqliteDiagnosticLogger(_connectionString);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Theory]
    [InlineData("https://example.com/file.zip?token=SUPER_SECRET_123&user=admin", "https://example.com/file.zip?token=[REDACTED]&user=admin")]
    [InlineData("Request failed with key=ABC999 and sig=XYZ888", "Request failed with key=[REDACTED] and sig=[REDACTED]")]
    [InlineData("Authorization header: Bearer mySecretJwtToken12345", "Authorization header: Bearer [REDACTED]")]
    public void RedactSensitiveData_ShouldRedactTokensAndKeys(string input, string expected)
    {
        string sanitized = _logger.RedactSensitiveData(input);
        sanitized.Should().Be(expected);
    }

    [Fact]
    public async Task LogEventAsync_ShouldPersistSanitizedEventInSqlite()
    {
        await _logger.LogEventAsync("Information", "User requested download with token=abc123secret");

        var events = await _logger.GetRecentEventsAsync(10);

        events.Should().ContainSingle();
        events[0].Level.Should().Be("Information");
        events[0].Message.Should().Be("User requested download with token=[REDACTED]");
    }
}
