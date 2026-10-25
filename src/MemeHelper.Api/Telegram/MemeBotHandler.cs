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
        if (!telegram.Value.AllowedUserIds.Contains(userId))
        {
            return;
        }

        var extension = "jpg";
        if (!isPhoto && !ImageFileRules.TryGetExtension(fileName ?? "", out extension))
        {
            await messenger.SendTextAsync(chatId, ReplyFormatter.UploadUnsupportedType, ct);
            return;
        }

        var keywords = KeywordNormalizer.Normalize([caption ?? ""]);
        if (keywords.Count == 0)
        {
            await messenger.SendTextAsync(chatId, ReplyFormatter.UploadNeedsKeywords, ct);
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

        var id = await repository.AddAsync(extension, keywords, ct);
        try
        {
            await images.SaveAsync(id, extension, new MemoryStream(content), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 存檔失敗就不能留下沒有圖的紀錄。
            logger.LogError(ex, "儲存編號 {Id} 的圖片失敗", id);
            await repository.DeleteAsync(id, ct);
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

        try
        {
            string newFileId;
            await using (stream)
            {
                newFileId = await messenger.SendPhotoFileAsync(chatId, stream, $"{meme.Id}.{meme.Extension}", ct);
            }

            await repository.SetTelegramFileIdAsync(meme.Id, newFileId, ct);
            logger.LogInformation("編號 {Id} 上傳回圖並快取 file_id", meme.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "編號 {Id} 送圖失敗", meme.Id);
            await messenger.SendTextAsync(chatId, ReplyFormatter.SendFailed, ct);
        }
    }
}
