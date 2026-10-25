using Dapper;
using MemeHelper.Api.Configuration;
using MemeHelper.Api.Database;
using MemeHelper.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MemeHelper.Tests.Database;

public class DatabaseMigratorTests
{
    [Fact]
    public async Task Migrate_對空資料庫執行_應建立memes與meme_keywords表()
    {
        using var db = new TempDatabase();

        CreateMigrator(db.ConnectionString).Migrate();

        await using var connection = new SqliteConnection(db.ConnectionString);
        var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('memes', 'meme_keywords')",
            cancellationToken: TestContext.Current.CancellationToken));
        count.Should().Be(2);
    }

    [Fact]
    public void Migrate_連續執行兩次_不應拋出例外()
    {
        using var db = new TempDatabase();
        CreateMigrator(db.ConnectionString).Migrate();

        var act = () => CreateMigrator(db.ConnectionString).Migrate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Migrate_連線字串指向不存在的目錄_應先建立目錄()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"memehelper-test-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directory, "nested", "app.db");
        try
        {
            CreateMigrator($"Data Source={filePath}").Migrate();

            File.Exists(filePath).Should().BeTrue();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static DatabaseMigrator CreateMigrator(string connectionString) =>
        new(
            Options.Create(new DatabaseOptions { ConnectionString = connectionString }),
            NullLogger<DatabaseMigrator>.Instance);
}
