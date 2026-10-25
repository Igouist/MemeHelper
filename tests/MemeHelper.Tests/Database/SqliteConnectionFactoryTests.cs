using Dapper;
using MemeHelper.Api.Configuration;
using MemeHelper.Api.Database;
using MemeHelper.Tests.Support;
using Microsoft.Extensions.Options;

namespace MemeHelper.Tests.Database;

public class SqliteConnectionFactoryTests
{
    [Fact]
    public async Task OpenAsync_開啟連線_journal_mode應為wal()
    {
        using var db = new TempDatabase();
        var factory = new SqliteConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = db.ConnectionString }));

        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        var mode = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "PRAGMA journal_mode", cancellationToken: TestContext.Current.CancellationToken));

        mode.Should().Be("wal");
    }

    [Fact]
    public async Task OpenAsync_開啟連線_foreign_keys應為開啟()
    {
        using var db = new TempDatabase();
        var factory = new SqliteConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = db.ConnectionString }));

        await using var connection = await factory.OpenAsync(TestContext.Current.CancellationToken);
        var enabled = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "PRAGMA foreign_keys", cancellationToken: TestContext.Current.CancellationToken));

        enabled.Should().Be(1);
    }
}
