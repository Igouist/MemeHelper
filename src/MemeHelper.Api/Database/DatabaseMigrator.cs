using DbUp;
using MemeHelper.Api.Configuration;
using Microsoft.Extensions.Options;

namespace MemeHelper.Api.Database;

/// <summary>
/// 以 DbUp 套用內嵌在組件裡的 Migrations/*.sql。在 Program 建好 host 之後、開始接收請求之前
/// 同步呼叫，失敗就讓啟動失敗。刻意不做成 hosted service，測試才能用
/// RemoveAll&lt;IHostedService&gt; 一次拿掉 Telegram polling 而不影響 schema。
/// </summary>
public sealed class DatabaseMigrator(IOptions<DatabaseOptions> options, ILogger<DatabaseMigrator> logger)
{
    public void Migrate()
    {
        var connectionString = options.Value.ConnectionString;
        EnsureDirectoryExists(connectionString);

        var upgrader = DeployChanges.To
            .SqliteDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrator).Assembly)
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException($"資料庫 migration 失敗：{result.Error.Message}", result.Error);
        }

        var applied = result.Scripts.Select(s => s.Name).ToList();
        if (applied.Count > 0)
        {
            logger.LogInformation("Migration 套用了 {Count} 個 script：{Scripts}", applied.Count, string.Join(", ", applied));
        }
    }

    private static void EnsureDirectoryExists(string connectionString)
    {
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
        var directory = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
