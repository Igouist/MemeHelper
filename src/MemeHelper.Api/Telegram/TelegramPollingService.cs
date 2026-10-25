using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MemeHelper.Api.Telegram;

/// <summary>
/// 用 long polling 向 Telegram 拉更新，只需要對外的 HTTPS 連線，不必開對內 port（ADR 0002）。
/// </summary>
public sealed class TelegramPollingService(
    ITelegramBotClient bot,
    MemeBotHandler handler,
    ILogger<TelegramPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message],
            // 服務停機期間累積的查詢都已過時，重啟後不該補回。
            DropPendingUpdates = true,
        };

        logger.LogInformation("Telegram long polling 開始");
        await bot.ReceiveAsync(HandleUpdateAsync, HandleErrorAsync, options, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken ct)
    {
        // 只處理文字；貼圖、圖片等其他訊息一律忽略。
        if (update.Message is not { Text: { } text, From: { } from, Chat: { } chat })
        {
            return;
        }

        try
        {
            await handler.HandleTextAsync(from.Id, chat.Id, text, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 單則訊息處理失敗不能讓 polling 停掉。
            logger.LogError(ex, "處理訊息失敗：{Text}", text);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient _, Exception exception, CancellationToken ct)
    {
        // Telegram.Bot 會自己重試，這裡只記錄。
        logger.LogError(exception, "Telegram polling 發生錯誤");
        return Task.CompletedTask;
    }
}
