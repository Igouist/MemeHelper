using System.Globalization;
using Dapper;
using MemeHelper.Api.Configuration;
using MemeHelper.Api.Database;
using Microsoft.Extensions.Options;

namespace MemeHelper.Api.Backup;

public sealed record BackupResult(string SnapshotPath, int CopiedImages, int PrunedSnapshots);

/// <summary>
/// 把 Data/ 複製到備份資料夾：資料庫存成帶時間的快照，圖片只補新的。
/// 備份資料夾內是 db/memehelper-{UTC 時間}.db 與 images/{編號}.{副檔名}。
/// </summary>
public sealed class DataBackup(
    IDbConnectionFactory connections,
    IOptions<StorageOptions> storage,
    IOptions<BackupOptions> backup,
    TimeProvider clock)
{
    private const string SnapshotPrefix = "memehelper-";
    private const string SnapshotExtension = ".db";
    private const string StampFormat = "yyyyMMdd'T'HHmmss'Z'";
    private const string PartialSuffix = ".partial";
    private const string SnapshotPartial = "memehelper.db" + PartialSuffix;

    private string SnapshotDir => Path.Combine(backup.Value.Directory, "db");

    private string ImagesDir => Path.Combine(backup.Value.Directory, "images");

    /// <summary>最新一份快照的時間，從檔名解析；沒有快照時為 null。</summary>
    public DateTimeOffset? LastBackupAt() =>
        Snapshots().Select(s => (DateTimeOffset?)s.Time).Max();

    public async Task<BackupResult> RunAsync(CancellationToken ct)
    {
        // 先做資料庫快照再複製圖片：快照裡引用到的圖，複製時都已經存在。
        var snapshot = await SnapshotDatabaseAsync(ct);
        var copied = CopyNewImages(ct);
        var pruned = PruneSnapshots();
        return new BackupResult(snapshot, copied, pruned);
    }

    private async Task<string> SnapshotDatabaseAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(SnapshotDir);
        var target = Path.Combine(SnapshotDir, $"{SnapshotPrefix}{clock.GetUtcNow().UtcDateTime.ToString(StampFormat, CultureInfo.InvariantCulture)}{SnapshotExtension}");

        // 寫到固定名稱的暫存檔再改名：中斷時只會留下這個檔，下次直接覆蓋，
        // 不會出現一份看起來完整、其實寫到一半的快照。
        var partial = Path.Combine(SnapshotDir, SnapshotPartial);
        File.Delete(partial);

        try
        {
            // 資料庫開著 WAL，直接複製檔案可能漏掉還在 -wal 裡的寫入或拿到寫一半的頁面；
            // VACUUM INTO 在一個讀取交易內輸出，結果是一致的快照，而且不會擋住寫入。
            await using (var connection = await connections.OpenAsync(ct))
            {
                await connection.ExecuteAsync(new CommandDefinition("VACUUM INTO @Path", new { Path = partial }, cancellationToken: ct));
            }

            File.Move(partial, target);
        }
        catch
        {
            File.Delete(partial);
            throw;
        }

        return target;
    }

    private int CopyNewImages(CancellationToken ct)
    {
        var source = storage.Value.ImageRoot;
        if (!Directory.Exists(source))
        {
            return 0;
        }

        Directory.CreateDirectory(ImagesDir);
        var copied = 0;
        foreach (var file in Directory.EnumerateFiles(source))
        {
            ct.ThrowIfCancellationRequested();

            // 編號不重用、圖片不會原地修改，所以同名就是同一張圖，不必比對內容。
            var target = Path.Combine(ImagesDir, Path.GetFileName(file));
            if (File.Exists(target))
            {
                continue;
            }

            var partial = target + PartialSuffix;
            File.Copy(file, partial, overwrite: true);
            File.Move(partial, target);
            copied++;
        }

        return copied;
    }

    private int PruneSnapshots()
    {
        var stale = Snapshots()
            .OrderByDescending(s => s.Time)
            .Skip(backup.Value.KeepCount)
            .ToList();

        foreach (var snapshot in stale)
        {
            File.Delete(snapshot.Path);
        }

        return stale.Count;
    }

    /// <summary>只認得檔名符合格式的快照，資料夾裡其他檔案一律不碰。</summary>
    private IEnumerable<(string Path, DateTimeOffset Time)> Snapshots()
    {
        if (!Directory.Exists(SnapshotDir))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(SnapshotDir, $"{SnapshotPrefix}*{SnapshotExtension}"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (DateTimeOffset.TryParseExact(
                    name[SnapshotPrefix.Length..],
                    StampFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var time))
            {
                yield return (path, time);
            }
        }
    }
}
