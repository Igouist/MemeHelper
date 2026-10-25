using Microsoft.Data.Sqlite;

namespace MemeHelper.Tests.Support;

/// <summary>
/// 一個臨時的 SQLite 檔案，測試結束後連同 WAL 副檔一起刪除。
/// 專案不對資料庫做抽象化，測試直接跑真的 SQL。
/// </summary>
public sealed class TempDatabase : IDisposable
{
    public TempDatabase()
    {
        FilePath = Path.Combine(Path.GetTempPath(), $"memehelper-test-{Guid.NewGuid():N}.db");
        ConnectionString = $"Data Source={FilePath}";
    }

    public string FilePath { get; }

    public string ConnectionString { get; }

    public void Dispose()
    {
        // 連線池會留住檔案 handle，不清掉 Windows 上刪不了檔。
        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = FilePath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
