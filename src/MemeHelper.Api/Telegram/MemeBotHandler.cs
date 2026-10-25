using System.Globalization;
using MemeHelper.Api.Configuration;
using MemeHelper.Api.Database;
using MemeHelper.Api.Features.Memes;
using MemeHelper.Api.Features.Search;
using Microsoft.Extensions.Options;

namespace MemeHelper.Api.Telegram;

public sealed class MemeBotHandler(
    IMemeRepository repository,
    MemeSearch search,
    IImageStore images,
    IBotMessenger messenger,
    IOptions<TelegramOptions> telegram,
    IOptions<SearchOptions> searchOptions,
    ILogger<MemeBotHandler> logger)
{
    public async Task HandleTextAsync(long userId, long chatId, string text, CancellationToken ct)
    {
        // 非白名單一律沉默，連「無權限」都不回，免得暴露這裡有個 bot 在聽。
        if (!telegram.Value.AllowedUserIds.Contains(userId))
        {
            return;
        }

        var trimmed = text.Trim();
        if (trimmed is "/start" or "/help")
        {
            await messenger.SendTextAsync(chatId, ReplyFormatter.Help, ct);
            return;
        }

        // 純數字先當編號；查無此編號（或已停用）才落到關鍵字搜尋，這樣「2077」這種數字關鍵字也找得到。
        if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            && await repository.GetAsync(id, ct) is { IsEnabled: true } byId)
        {
            await SendMemeAsync(chatId, byId, ct);
            return;
        }

        if (KeywordMatcher.ParseTerms(trimmed).Count == 0)
        {
            await messenger.SendTextAsync(chatId, ReplyFormatter.NotFound, ct);
            return;
        }

        var found = await search.FindAsync(trimmed, includeDisabled: false, ct);
        switch (found.Count)
        {
            case 0:
                await messenger.SendTextAsync(chatId, ReplyFormatter.NotFound, ct);
                break;
            case 1:
                await SendMemeAsync(chatId, found[0], ct);
                break;
            default:
                await messenger.SendTextAsync(chatId, ReplyFormatter.FormatList(found, searchOptions.Value.MaxListResults), ct);
                break;
        }
    }

    private async Task SendMemeAsync(long chatId, Meme meme, CancellationToken ct)
    {
        if (meme.TelegramFileId is { } fileId)
        {
            try
            {
                await messenger.SendPhotoByFileIdAsync(chatId, fileId, ct);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // file_id 綁定 bot，換過 bot 或 Telegram 清掉時會失效；退回用檔案上傳並換新的 ID。
                logger.LogWarning(ex, "編號 {Id} 的 file_id 重送失敗，改用檔案上傳", meme.Id);
            }
        }

        var stream = images.OpenRead(meme.Id, meme.Extension);
        if (stream is null)
        {
            logger.LogError("編號 {Id} 的圖片檔不存在", meme.Id);
            await messenger.SendTextAsync(chatId, string.Format(CultureInfo.InvariantCulture, ReplyFormatter.ImageMissing, meme.Id), ct);
            return;
        }

        try
        {
            string newFileId;
            await using (stream)
            {
                newFileId = await messenger.SendPhotoFileAsync(chatId, stream, $"{meme.Id}.{meme.Extension}", ct);
            }

            await repository.SetTelegramFileIdAsync(meme.Id, newFileId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "編號 {Id} 送圖失敗", meme.Id);
            await messenger.SendTextAsync(chatId, ReplyFormatter.SendFailed, ct);
        }
    }
}
