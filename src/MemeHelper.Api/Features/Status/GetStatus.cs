using MemeHelper.Api.Backup;
using MemeHelper.Api.Database;
using MemeHelper.Api.Telegram;

namespace MemeHelper.Api.Features.Status;

public sealed record ServiceStatus(int MemeCount, int EnabledCount, DateTimeOffset? LastPollingSuccessAt, DateTimeOffset? LastBackupAt);

/// <summary>一眼看出服務活著沒有，不必翻 log。</summary>
internal static class GetStatus
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/status", Handle).WithTags("Status").Produces<ServiceStatus>();

    private static async Task<ServiceStatus> Handle(
        IMemeRepository repository, PollingHealth polling, DataBackup backup, CancellationToken ct)
    {
        // 數百張的規模整批讀出來數就好，不為狀態頁多一個 COUNT 查詢。
        var memes = await repository.ListAsync(includeDisabled: true, ct);
        return new ServiceStatus(memes.Count, memes.Count(m => m.IsEnabled), polling.LastSuccessAt, backup.LastBackupAt());
    }
}
