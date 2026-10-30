using MemeHelper.Api.Configuration;
using Microsoft.Extensions.Options;

namespace MemeHelper.Api.Backup;

/// <summary>
/// 每 24 小時備份一次。下一次的時間從最新快照的檔名推算，所以重啟不會多備份，
/// 也不會把排程往後推；備份失敗只記 log 並稍後重試，不影響 bot 與後台。
/// </summary>
public sealed class BackupService(
    DataBackup backup,
    IOptions<BackupOptions> options,
    TimeProvider clock,
    ILogger<BackupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var directory = options.Value.Directory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            logger.LogInformation("未設定 Backup:Directory，不做自動備份");
            return;
        }

        logger.LogInformation("自動備份到 {Directory}，每 {Hours} 小時一次", Path.GetFullPath(directory), Interval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilDue(backup.LastBackupAt(), clock.GetUtcNow());
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, clock, stoppingToken);
            }

            try
            {
                var result = await backup.RunAsync(stoppingToken);
                logger.LogInformation(
                    "備份完成：{Snapshot}，新增 {Copied} 張圖，清掉 {Pruned} 份舊快照",
                    Path.GetFileName(result.SnapshotPath),
                    result.CopiedImages,
                    result.PrunedSnapshots);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "備份失敗，{Hours} 小時後重試", RetryDelay.TotalHours);
                await Task.Delay(RetryDelay, clock, stoppingToken);
            }
        }
    }

    /// <summary>
    /// 最多等一個間隔：設備時鐘倒退時最新快照會在「未來」，不設上限會一直等下去，
    /// 超過 Task.Delay 的上限（約 49 天）還會丟例外讓整個 host 停掉。
    /// </summary>
    private static TimeSpan DelayUntilDue(DateTimeOffset? last, DateTimeOffset now)
    {
        if (last is null)
        {
            return TimeSpan.Zero;
        }

        var delay = last.Value + Interval - now;
        return delay > Interval ? Interval : delay;
    }
}
