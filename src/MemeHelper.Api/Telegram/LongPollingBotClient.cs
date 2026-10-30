using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;

namespace MemeHelper.Api.Telegram;

/// <summary>
/// Telegram.Bot 的 ReceiveAsync 把 getUpdates 的伺服器等待秒數設成和 HttpClient.Timeout 一樣（預設 100 秒）。
/// 沒有新訊息時伺服器會等滿才回，client 卻在同一刻先逾時，每段閒置都會冒一次「Request timed out」。
/// 這裡把伺服器等待秒數縮短，留餘裕給網路延遲。
/// 另外每次 getUpdates 成功都記到 PollingHealth：ReceiveAsync 只在有訊息時才呼叫 handler，
/// 閒置的空回應只有這裡看得到。
/// </summary>
public sealed class LongPollingBotClient(string token, PollingHealth health, HttpClient? httpClient = null) : TelegramBotClient(token, httpClient)
{
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(10);

    public override async Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is GetUpdatesRequest { Timeout: > 0 } getUpdates)
        {
            var window = TimeSpan.FromSeconds(getUpdates.Timeout.Value) - Margin;
            getUpdates.Timeout = (int)Math.Max(1, window.TotalSeconds);
        }

        var response = await base.SendRequest(request, cancellationToken);
        if (request is GetUpdatesRequest)
        {
            health.MarkSuccess();
        }

        return response;
    }
}
