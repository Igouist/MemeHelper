using System.Data.Common;
using MemeHelper.Api.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace MemeHelper.Api.Database;

/// <summary>
/// 開啟連線並套用 PRAGMA。WAL 讓讀取不阻塞寫入，busy_timeout 讓寫入衝突時自動重試，
/// foreign_keys 讓刪除梗圖時 meme_keywords 跟著級聯刪除。
/// </summary>
public sealed class SqliteConnectionFactory(IOptions<DatabaseOptions> options) : IDbConnectionFactory
{
    private const string Pragmas = """
        PRAGMA journal_mode=WAL;
        PRAGMA busy_timeout=5000;
        PRAGMA foreign_keys=ON;
        """;

    public async Task<DbConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(options.Value.ConnectionString);

        try
        {
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = Pragmas;
            await command.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // PRAGMA 失敗或中途取消時連線不會交給呼叫端，要在這裡釋放才不會洩漏。
            await connection.DisposeAsync();
            throw;
        }

        return connection;
    }
}
