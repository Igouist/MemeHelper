namespace MemeHelper.Api.Telegram;

/// <summary>
/// Bot 邏輯只透過這三個方法碰 Telegram。Telegram.Bot 的送訊息方法都是 extension method，
/// 無法做替身，所以包成自己的介面讓 MemeBotHandler 能用 NSubstitute 測。
/// </summary>
public interface IBotMessenger
{
    Task SendTextAsync(long chatId, string text, CancellationToken ct);

    /// <summary>上傳檔案送圖，回傳 Telegram 給的 file_id。</summary>
    Task<string> SendPhotoFileAsync(long chatId, Stream content, string fileName, CancellationToken ct);

    Task SendPhotoByFileIdAsync(long chatId, string fileId, CancellationToken ct);
}
