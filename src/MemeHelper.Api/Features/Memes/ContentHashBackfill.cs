using MemeHelper.Api.Database;

namespace MemeHelper.Api.Features.Memes;

/// <summary>
/// 替去重功能上線前新增的梗圖補算內容雜湊。在 Program 跑完 migration 後、開始接收請求前
/// 呼叫一次；沒有缺雜湊的紀錄時什麼都不做，所以每次啟動都跑也沒有成本。
/// 不寫在 migration 裡：migration 是純 SQL，讀不到圖片檔。
/// </summary>
public sealed class ContentHashBackfill(IMemeRepository repository, IImageStore images, ILogger<ContentHashBackfill> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var missing = await repository.ListWithoutContentHashAsync(ct);
        if (missing.Count == 0)
        {
            return;
        }

        var filled = 0;
        foreach (var meme in missing)
        {
            string hash;
            var stream = images.OpenRead(meme.Id, meme.Extension);
            if (stream is null)
            {
                logger.LogWarning("編號 {Id} 的圖片檔不存在，無法補算雜湊", meme.Id);
                continue;
            }

            await using (stream)
            {
                hash = await ContentHash.ComputeAsync(stream, ct);
            }

            // 舊資料裡可能已經有重複的圖；雜湊有唯一索引，後面那張留空讓使用者自己處理。
            if (await repository.FindByContentHashAsync(hash, ct) is { } duplicate)
            {
                logger.LogWarning("編號 {Id} 與編號 {DuplicateId} 內容相同，雜湊留空，請刪除其中一張", meme.Id, duplicate.Id);
                continue;
            }

            await repository.SetContentHashAsync(meme.Id, hash, ct);
            filled++;
        }

        logger.LogInformation("補算了 {Filled} 張梗圖的內容雜湊，共 {Missing} 張缺少", filled, missing.Count);
    }
}
