using Telegram.Bot;
using Telegram.Bot.Types;

namespace MemeHelper.Api.Telegram;

/// <summary>對 Telegram.Bot 的薄包裝，沒有單元測試，正確性靠真機驗證。</summary>
public sealed class TelegramBotMessenger(ITelegramBotClient bot) : IBotMessenger
{
    public Task SendTextAsync(long chatId, string text, CancellationToken ct) =>
        bot.SendMessage(chatId, text, cancellationToken: ct);

    public async Task<string> SendPhotoFileAsync(long chatId, Stream content, string fileName, CancellationToken ct)
    {
        var message = await bot.SendPhoto(chatId, InputFile.FromStream(content, fileName), cancellationToken: ct);

        // Telegram 會存多種尺寸，陣列最後一個最大；用它重送才不會變模糊。
        var photo = message.Photo ?? throw new InvalidOperationException("Telegram 回應裡沒有 photo");
        return photo[^1].FileId;
    }

    public Task SendPhotoByFileIdAsync(long chatId, string fileId, CancellationToken ct) =>
        bot.SendPhoto(chatId, InputFile.FromFileId(fileId), cancellationToken: ct);
}
