using FluentAssertions;
using Microsoft.Data.Sqlite;
using Velo.Infrastructure.Database;
using Xunit;

namespace Velo.IntegrationTests;

public class SqliteDatabaseInitializerTests
{
    [Fact]
    public void Initialize_ShouldCreateAllRequiredTablesInMemory()
    {
        // Arrange
        using var connection = new SqliteConnection("Data Source=:memory:;Mode=Memory;Cache=Shared");
        connection.Open();

        // Act
        SqliteDatabaseInitializer.Initialize(connection.ConnectionString);

        // Assert
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('downloads', 'segments', 'settings', 'diagnostic_events');";
        using var reader = cmd.ExecuteReader();

        var tableNames = new List<string>();
        while (reader.Read())
        {
            tableNames.Add(reader.GetString(0));
        }

        tableNames.Should().Contain(["downloads", "segments", "settings", "diagnostic_events"]);
    }
}
