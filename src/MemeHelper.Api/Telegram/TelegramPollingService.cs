using Telegram.Bot;
using Telegram.Bot.Exceptions;
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
    TimeProvider clock,
    ILogger<TelegramPollingService> logger) : BackgroundService
{
    /// <summary>ReceiveAsync 整個失敗（例如開機時網路還沒好、token 無效）後，隔多久重新開始。</summary>
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(10);

    /// <summary>單次 getUpdates 失敗後的退避。Telegram.Bot 失敗後會立刻重試（實測約 0.2 秒一次），斷網時要自己放慢。</summary>
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message],
            // 服務停機期間累積的查詢都已過時，重啟後不該補回。
            DropPendingUpdates = true,
        };

        // DropPendingUpdates 的第一次 getUpdates 不在 Telegram.Bot 的錯誤處理迴圈內，失敗會直接從
        // ReceiveAsync 拋出。不接住的話 BackgroundService 會停掉整個 host，連區網後台一起死，
        // 所以這裡自己重試：polling 壞了只影響 bot，後台照常。
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Telegram long polling 開始");
                await bot.ReceiveAsync(HandleUpdateAsync, HandleErrorAsync, options, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Telegram polling 中斷，{Delay} 秒後重新開始", RestartDelay.TotalSeconds);
                await Task.Delay(RestartDelay, clock, stoppingToken);
            }
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken ct)
    {
        if (update.Message is not { From: { } from, Chat: { } chat } message)
        {
            return;
        }

        try
        {
            // 文字是查詢；照片與檔案是新增；貼圖等其他訊息一律忽略。
            if (message.Text is { } text)
            {
                await handler.HandleTextAsync(from.Id, chat.Id, text, ct);
            }
            else if (message.Photo is { Length: > 0 } photo)
            {
                // 陣列最後一個是最大尺寸。
                await handler.HandleImageAsync(from.Id, chat.Id, photo[^1].FileId, null, isPhoto: true, message.Caption, ct);
            }
            else if (message.Document is { } document)
            {
                await handler.HandleImageAsync(from.Id, chat.Id, document.FileId, document.FileName, isPhoto: false, message.Caption, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 單則訊息處理失敗不能讓 polling 停掉。
            logger.LogError(ex, "處理訊息 {MessageId} 失敗", message.Id);
        }
    }

    private async Task HandleErrorAsync(ITelegramBotClient _, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Telegram polling 發生錯誤");
        if (exception is RequestException)
        {
            await Task.Delay(ErrorBackoff, clock, ct);
        }
    }
}
