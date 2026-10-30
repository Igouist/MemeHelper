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
        if (!IsAllowed(userId))
        {
            return;
        }

        var trimmed = text.Trim();
        if (trimmed is "/start" or "/help")
        {
            await messenger.SendTextAsync(chatId, ReplyFormatter.Help, ct);
            return;
        }

        if (KeywordCommand.TryParse(trimmed) is { } command)
        {
            await HandleCommandAsync(userId, chatId, command, ct);
            return;
        }

        // 純數字先當編號；查無此編號（或已停用）才落到關鍵字搜尋，這樣「2077」這種數字關鍵字也找得到。
        if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            && await repository.GetAsync(id, ct) is { IsEnabled: true } byId)
        {
            logger.LogInformation("使用者 {UserId} 以編號 {Id} 取圖", userId, id);
            await SendMemeAsync(chatId, byId, ct);
            return;
        }

        if (KeywordMatcher.ParseTerms(trimmed).Count == 0)
        {
            await messenger.SendTextAsync(chatId, ReplyFormatter.NotFound, ct);
            return;
        }

        var found = await search.FindAsync(trimmed, includeDisabled: false, ct);
        logger.LogInformation("使用者 {UserId} 查詢「{Query}」命中 {Count} 張", userId, trimmed, found.Count);
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

    /// <summary>
    /// 使用者傳圖給 bot、caption 填關鍵字。isPhoto 為 true 是 Telegram 壓縮過的照片（一律 JPEG），
    /// false 是「以檔案傳送」的原始檔。
    /// </summary>
    public async Task HandleImageAsync(
        long userId, long chatId, string fileId, string? fileName, bool isPhoto, string? caption, CancellationToken ct)
    {
        if (!IsAllowed(userId))
        {
            return;
        }

        var extension = "jpg";
        if (!isPhoto && !ImageFileRules.TryGetExtension(fileName ?? "", out extension))
        {
            await messenger.SendTextAsync(chatId, ReplyFormatter.UploadUnsupportedType, ct);
            return;
        }

        byte[] content;
        try
        {
            content = await messenger.DownloadFileAsync(fileId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "下載 Telegram 檔案失敗");
            await messenger.SendTextAsync(chatId, ReplyFormatter.UploadFailed, ct);
            return;
        }

        if (content.Length > ImageFileRules.MaxBytes)
        {
            await messenger.SendTextAsync(chatId, ReplyFormatter.UploadTooLarge, ct);
            return;
        }

        if (!ImageFileRules.HasValidSignature(content, extension))
        {
            await messenger.SendTextAsync(chatId, string.Format(CultureInfo.InvariantCulture, ReplyFormatter.UploadBadContent, extension), ct);
            return;
        }

        // 查重在關鍵字檢查之前：不填 caption 直接傳圖，就能查這張圖收過沒有。
        var contentHash = ContentHash.Compute(content);
        if (await repository.FindByContentHashAsync(contentHash, ct) is { } existing)
        {
            logger.LogInformation("使用者 {UserId} 傳的圖已存在，編號 {Id}", userId, existing.Id);
            await messenger.SendTextAsync(chatId, ReplyFormatter.FormatExists(existing), ct);
            return;
        }

        var keywords = KeywordNormalizer.Normalize([caption ?? ""]);
        if (keywords.Count == 0)
        {
            await messenger.SendTextAsync(chatId, ReplyFormatter.UploadNeedsKeywords, ct);
            return;
        }

        var id = await repository.AddAsync(extension, contentHash, keywords, ct);
        try
        {
            await images.SaveAsync(id, extension, new MemoryStream(content), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 存檔失敗就不能留下沒有圖的紀錄。補償不帶 ct：取消時更要清乾淨。
            logger.LogError(ex, "儲存編號 {Id} 的圖片失敗", id);
            await repository.DeleteAsync(id, CancellationToken.None);
            images.Delete(id, extension);
            await messenger.SendTextAsync(chatId, ReplyFormatter.UploadFailed, ct);
            return;
        }

        // 照片的 file_id 可以直接用 sendPhoto 重送；檔案的不行（Telegram 不允許重送時改型別），
        // 所以不存，第一次查詢時再上傳並快取。
        if (isPhoto)
        {
            await repository.SetTelegramFileIdAsync(id, fileId, ct);
        }

        var meme = await repository.GetAsync(id, ct);
        logger.LogInformation(
            "使用者 {UserId} 從 Telegram 新增編號 {Id}：{Keywords}（{Kind}）",
            userId, id, string.Join(",", keywords), isPhoto ? "照片" : "檔案");
        await messenger.SendTextAsync(chatId, ReplyFormatter.FormatAdded(meme!), ct);
    }

    /// <summary>非白名單一律沉默，連「無權限」都不回，免得暴露這裡有個 bot 在聽；只記 log 讓自己知道有人來過。</summary>
    private bool IsAllowed(long userId)
    {
        if (telegram.Value.AllowedUserIds.Contains(userId))
        {
            return true;
        }

        logger.LogWarning("忽略非白名單使用者 {UserId} 的訊息", userId);
        return false;
    }

    private async Task HandleCommandAsync(long userId, long chatId, KeywordCommand command, CancellationToken ct)
    {
        switch (command)
        {
            case KeywordCommand.Invalid invalid:
                await messenger.SendTextAsync(chatId, invalid.Usage, ct);
                return;

            case KeywordCommand.Add add:
                if (!await repository.AddKeywordsAsync(add.MemeId, add.Keywords, ct))
                {
                    await messenger.SendTextAsync(chatId, string.Format(CultureInfo.InvariantCulture, ReplyFormatter.MemeNotFound, add.MemeId), ct);
                    return;
                }

                logger.LogInformation("使用者 {UserId} 替編號 {Id} 新增關鍵字：{Keywords}", userId, add.MemeId, string.Join(",", add.Keywords));
                await messenger.SendTextAsync(chatId, ReplyFormatter.FormatKeywords((await repository.GetAsync(add.MemeId, ct))!), ct);
                return;

            case KeywordCommand.Remove remove:
                var missing = new List<string>();
                foreach (var keyword in remove.Keywords)
                {
                    switch (await repository.RemoveKeywordAsync(remove.MemeId, keyword, ct))
                    {
                        case RemoveKeywordResult.MemeNotFound:
                            await messenger.SendTextAsync(chatId, string.Format(CultureInfo.InvariantCulture, ReplyFormatter.MemeNotFound, remove.MemeId), ct);
                            return;
                        case RemoveKeywordResult.KeywordNotFound:
                            missing.Add(keyword);
                            break;
                    }
                }

                logger.LogInformation("使用者 {UserId} 替編號 {Id} 移除關鍵字：{Keywords}", userId, remove.MemeId, string.Join(",", remove.Keywords));
                await messenger.SendTextAsync(chatId, ReplyFormatter.FormatRemoved((await repository.GetAsync(remove.MemeId, ct))!, missing), ct);
                return;
        }
    }

    private async Task SendMemeAsync(long chatId, Meme meme, CancellationToken ct)
    {
        if (meme.TelegramFileId is { } fileId)
        {
            try
            {
                await messenger.SendPhotoByFileIdAsync(chatId, fileId, ct);
                logger.LogInformation("編號 {Id} 以 file_id 回圖", meme.Id);
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

        string newFileId;
        try
        {
            await using (stream)
            {
                newFileId = await messenger.SendPhotoFileAsync(chatId, stream, $"{meme.Id}.{meme.Extension}", ct);
            }

            logger.LogInformation("編號 {Id} 上傳回圖", meme.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "編號 {Id} 送圖失敗", meme.Id);
            await messenger.SendTextAsync(chatId, ReplyFormatter.SendFailed, ct);
            return;
        }

        // 圖已經送到了，快取 file_id 失敗只影響下次要不要重新上傳，不能回「送圖失敗」。
        try
        {
            await repository.SetTelegramFileIdAsync(meme.Id, newFileId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "編號 {Id} 的 file_id 快取失敗，下次查詢會再上傳一次", meme.Id);
        }
    }
}
