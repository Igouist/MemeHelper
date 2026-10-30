using Telegram.Bot.Types;

namespace MemeHelper.Api.Telegram;

/// <summary>
/// 一則 Telegram 訊息對 bot 的意義：文字是查詢，照片與檔案是新增，其他一律忽略。
/// 純函式，判斷規則在這裡測，polling service 只負責轉交。
/// </summary>
public abstract record IncomingMessage
{
    public sealed record Text(long UserId, long ChatId, string Content) : IncomingMessage;

    public sealed record Image(long UserId, long ChatId, string FileId, string? FileName, bool IsPhoto, string? Caption) : IncomingMessage;

    /// <summary>沒有對應意義時回 null，例如貼圖、頻道貼文（沒有寄件者）。</summary>
    public static IncomingMessage? From(Message message)
    {
        if (message.From is not { } from)
        {
            return null;
        }

        return message switch
        {
            { Text: { } text } => new Text(from.Id, message.Chat.Id, text),
            // 陣列最後一個是最大尺寸。
            { Photo: { Length: > 0 } photo } => new Image(from.Id, message.Chat.Id, photo[^1].FileId, null, IsPhoto: true, message.Caption),
            { Document: { } document } => new Image(from.Id, message.Chat.Id, document.FileId, document.FileName, IsPhoto: false, message.Caption),
            _ => null,
        };
    }
}
