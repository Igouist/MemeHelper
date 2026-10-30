using System.Globalization;
using MemeHelper.Api.Backup;
using MemeHelper.Api.Configuration;
using MemeHelper.Api.Database;
using MemeHelper.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace MemeHelper.Tests.Backup;

public sealed class BackupServiceTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 3, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);

    private readonly TempDatabase db = new();
    private readonly TempDirectory root = new();
    private readonly SqliteConnectionFactory connections;
    private readonly FakeTimeProvider clock = new(Start);
    private readonly RecordingLogger<BackupService> log = new();

    public BackupServiceTests()
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = db.ConnectionString });
        new DatabaseMigrator(options, NullLogger<DatabaseMigrator>.Instance).Migrate();
        connections = new SqliteConnectionFactory(options);
    }

    private string BackupRoot => Path.Combine(root.Path, "backup");

    private string SnapshotDir => Path.Combine(BackupRoot, "db");

    public void Dispose()
    {
        db.Dispose();
        root.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_未設定備份資料夾_應記錄說明且不備份()
    {
        var service = Service(directory: "");

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        await service.StopAsync(CancellationToken.None);

        log.Has(LogLevel.Information, "Backup:Directory").Should().BeTrue();
        Directory.Exists(BackupRoot).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_從沒備份過_啟動後應立刻備份()
    {
        var service = Service();

        await service.StartAsync(CancellationToken.None);
        var appeared = SpinWait.SpinUntil(() => SnapshotTimes().Count >= 1, WaitLimit);
        await service.StopAsync(CancellationToken.None);

        appeared.Should().BeTrue("沒有快照時不該等，啟動就要備份");
        SnapshotTimes().Should().Equal(Start);
    }

    [Fact]
    public async Task ExecuteAsync_上次備份未滿24小時_應等滿24小時才備份()
    {
        var last = Start.AddHours(-10);
        Directory.CreateDirectory(SnapshotDir);
        await File.WriteAllBytesAsync(SnapshotPathAt(last), [], TestContext.Current.CancellationToken);
        var service = Service();

        await service.StartAsync(CancellationToken.None);
        AdvanceUntil(() => SnapshotTimes().Count >= 2, TimeSpan.FromHours(1));
        await service.StopAsync(CancellationToken.None);

        SnapshotTimes().Max().Should().BeOnOrAfter(last.AddHours(24));
    }

    [Fact]
    public async Task ExecuteAsync_最新快照時間在未來_最多等24小時就備份()
    {
        // 設備時鐘倒退（例如開機後還沒對時）時，舊快照的時間會比現在晚。
        Directory.CreateDirectory(SnapshotDir);
        await File.WriteAllBytesAsync(SnapshotPathAt(Start.AddDays(100)), [], TestContext.Current.CancellationToken);
        var service = Service();

        await service.StartAsync(CancellationToken.None);
        AdvanceUntil(() => SnapshotTimes().Count >= 2, TimeSpan.FromHours(1));
        await service.StopAsync(CancellationToken.None);

        service.ExecuteTask!.IsFaulted.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_備份失敗_應記錄錯誤並在一小時後重試()
    {
        // 備份資料夾的位置被一個檔案佔住，建立資料夾必定失敗。
        Directory.CreateDirectory(root.Path);
        await File.WriteAllTextAsync(BackupRoot, "", TestContext.Current.CancellationToken);
        var service = Service();

        await service.StartAsync(CancellationToken.None);
        SpinWait.SpinUntil(() => log.Has(LogLevel.Error, "備份失敗"), WaitLimit).Should().BeTrue("第一次備份應失敗並記錄");
        File.Delete(BackupRoot);
        AdvanceUntil(() => SnapshotTimes().Count >= 1, TimeSpan.FromMinutes(10));
        await service.StopAsync(CancellationToken.None);

        service.ExecuteTask!.IsFaulted.Should().BeFalse();
        SnapshotTimes().Single().Should().BeOnOrAfter(Start.AddHours(1));
    }

    private BackupService Service(string? directory = null)
    {
        var options = Options.Create(new BackupOptions { Directory = directory ?? BackupRoot });
        var backup = new DataBackup(
            connections,
            Options.Create(new StorageOptions { ImageRoot = Path.Combine(root.Path, "source-images") }),
            options,
            clock);
        return new BackupService(backup, options, clock, log);
    }

    private string SnapshotPathAt(DateTimeOffset time) =>
        Path.Combine(SnapshotDir, $"memehelper-{time.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}.db");

    private List<DateTimeOffset> SnapshotTimes() =>
        !Directory.Exists(SnapshotDir)
            ? []
            : Directory.GetFiles(SnapshotDir, "memehelper-*.db")
                .Select(p => DateTimeOffset.ParseExact(
                    Path.GetFileNameWithoutExtension(p)["memehelper-".Length..],
                    "yyyyMMdd'T'HHmmss'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal))
                .ToList();

    /// <summary>
    /// 服務在背景執行緒上才向假時鐘註冊計時器，單次 Advance 可能搶在它之前，
    /// 所以邊等邊推進。斷言因此只看「最早在什麼時間之後」，不看確切時間。
    /// </summary>
    private void AdvanceUntil(Func<bool> condition, TimeSpan step) =>
        SpinWait.SpinUntil(
            () =>
            {
                clock.Advance(step);
                return condition();
            },
            WaitLimit).Should().BeTrue("推進時鐘後應該要產生快照");
}
