using Dapper;
using MemeHelper.Api.Backup;
using MemeHelper.Api.Configuration;
using MemeHelper.Api.Database;
using MemeHelper.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace MemeHelper.Tests.Backup;

public sealed class DataBackupTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 3, 0, 0, TimeSpan.Zero);

    private readonly TempDatabase db = new();
    private readonly TempDirectory root = new();
    private readonly SqliteConnectionFactory connections;
    private readonly FakeTimeProvider clock = new(Now);

    public DataBackupTests()
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = db.ConnectionString });
        new DatabaseMigrator(options, NullLogger<DatabaseMigrator>.Instance).Migrate();
        connections = new SqliteConnectionFactory(options);
        Directory.CreateDirectory(SourceImages);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string SourceImages => Path.Combine(root.Path, "source-images");

    private string BackupRoot => Path.Combine(root.Path, "backup");

    private string SnapshotDir => Path.Combine(BackupRoot, "db");

    private string BackupImages => Path.Combine(BackupRoot, "images");

    public void Dispose()
    {
        db.Dispose();
        root.Dispose();
    }

    [Fact]
    public async Task RunAsync_首次備份_應在db資料夾產生以當下UTC時間命名的快照()
    {
        var result = await Backup().RunAsync(Ct);

        result.SnapshotPath.Should().Be(Path.Combine(SnapshotDir, "memehelper-20261029T030000Z.db"));
        File.Exists(result.SnapshotPath).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_寫入還在WAL裡_快照應能開啟且包含所有梗圖()
    {
        var repository = new SqliteMemeRepository(connections, clock);
        await repository.AddAsync("png", "hash-1", ["六點", "下班"], Ct);
        await repository.AddAsync("jpg", "hash-2", ["神奇海螺"], Ct);

        var result = await Backup().RunAsync(Ct);

        // 不用連線池，測試結束時才刪得掉快照檔。
        await using var snapshot = new SqliteConnection($"Data Source={result.SnapshotPath};Mode=ReadOnly;Pooling=False");
        var memes = await snapshot.ExecuteScalarAsync<long>(new CommandDefinition("SELECT COUNT(*) FROM memes", cancellationToken: Ct));
        var keywords = await snapshot.ExecuteScalarAsync<long>(new CommandDefinition("SELECT COUNT(*) FROM meme_keywords", cancellationToken: Ct));
        memes.Should().Be(2);
        keywords.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_來源有新圖_應複製到備份的images資料夾且內容相同()
    {
        await File.WriteAllBytesAsync(Path.Combine(SourceImages, "1.png"), SampleImages.Png(), Ct);

        var result = await Backup().RunAsync(Ct);

        result.CopiedImages.Should().Be(1);
        (await File.ReadAllBytesAsync(Path.Combine(BackupImages, "1.png"), Ct)).Should().Equal(SampleImages.Png());
    }

    [Fact]
    public async Task RunAsync_備份已有部分圖片_應只複製新的且保留來源已刪除的圖()
    {
        Directory.CreateDirectory(BackupImages);
        await File.WriteAllBytesAsync(Path.Combine(BackupImages, "1.png"), SampleImages.Png(), Ct);
        await File.WriteAllBytesAsync(Path.Combine(BackupImages, "2.png"), SampleImages.Png(), Ct);
        await File.WriteAllBytesAsync(Path.Combine(SourceImages, "1.png"), SampleImages.Png(), Ct);
        await File.WriteAllBytesAsync(Path.Combine(SourceImages, "3.jpg"), SampleImages.Jpg(), Ct);

        var result = await Backup().RunAsync(Ct);

        result.CopiedImages.Should().Be(1);
        Directory.GetFiles(BackupImages).Select(Path.GetFileName).Should().BeEquivalentTo(["1.png", "2.png", "3.jpg"]);
    }

    [Fact]
    public async Task RunAsync_有上次中斷留下的partial檔_應覆寫並完成複製()
    {
        Directory.CreateDirectory(BackupImages);
        Directory.CreateDirectory(SnapshotDir);
        await File.WriteAllBytesAsync(Path.Combine(BackupImages, "1.png.partial"), [1, 2, 3], Ct);
        await File.WriteAllBytesAsync(Path.Combine(SnapshotDir, "memehelper.db.partial"), [1, 2, 3], Ct);
        await File.WriteAllBytesAsync(Path.Combine(SourceImages, "1.png"), SampleImages.Png(), Ct);

        await Backup().RunAsync(Ct);

        (await File.ReadAllBytesAsync(Path.Combine(BackupImages, "1.png"), Ct)).Should().Equal(SampleImages.Png());
        Directory.GetFiles(BackupRoot, "*.partial", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_來源圖片資料夾不存在_應只備份資料庫()
    {
        Directory.Delete(SourceImages);

        var result = await Backup().RunAsync(Ct);

        result.CopiedImages.Should().Be(0);
        File.Exists(result.SnapshotPath).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_快照超過保留份數_應只刪除最舊的快照()
    {
        Directory.CreateDirectory(SnapshotDir);
        for (var daysAgo = 14; daysAgo >= 1; daysAgo--)
        {
            await File.WriteAllBytesAsync(SnapshotPathAt(Now.AddDays(-daysAgo)), [], Ct);
        }

        var unrelated = Path.Combine(SnapshotDir, "notes.txt");
        await File.WriteAllTextAsync(unrelated, "自己放的檔案", Ct);

        var result = await Backup(keepCount: 14).RunAsync(Ct);

        result.PrunedSnapshots.Should().Be(1);
        Directory.GetFiles(SnapshotDir, "memehelper-*.db").Should().HaveCount(14);
        File.Exists(SnapshotPathAt(Now.AddDays(-14))).Should().BeFalse();
        File.Exists(result.SnapshotPath).Should().BeTrue();
        File.Exists(unrelated).Should().BeTrue();
    }

    [Fact]
    public void LastBackupAt_沒有快照_應為null()
    {
        Backup().LastBackupAt().Should().BeNull();
    }

    [Fact]
    public async Task LastBackupAt_有多份快照_應回最新一份的時間且忽略其他檔案()
    {
        Directory.CreateDirectory(SnapshotDir);
        await File.WriteAllBytesAsync(SnapshotPathAt(Now.AddDays(-2)), [], Ct);
        await File.WriteAllBytesAsync(SnapshotPathAt(Now.AddHours(-10)), [], Ct);
        await File.WriteAllBytesAsync(Path.Combine(SnapshotDir, "memehelper.db.partial"), [], Ct);
        await File.WriteAllBytesAsync(Path.Combine(SnapshotDir, "memehelper-not-a-time.db"), [], Ct);

        Backup().LastBackupAt().Should().Be(Now.AddHours(-10));
    }

    private string SnapshotPathAt(DateTimeOffset time) =>
        Path.Combine(SnapshotDir, $"memehelper-{time.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}.db");

    private DataBackup Backup(int keepCount = 14) =>
        new(
            connections,
            Options.Create(new StorageOptions { ImageRoot = SourceImages }),
            Options.Create(new BackupOptions { Directory = BackupRoot, KeepCount = keepCount }),
            clock);
}
