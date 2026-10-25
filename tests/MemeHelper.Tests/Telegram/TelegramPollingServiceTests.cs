using MemeHelper.Api.Configuration;
using MemeHelper.Api.Database;
using MemeHelper.Api.Features.Memes;
using MemeHelper.Api.Features.Search;
using MemeHelper.Api.Telegram;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;

namespace MemeHelper.Tests.Telegram;

public class TelegramPollingServiceTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);

    private readonly ITelegramBotClient bot = Substitute.For<ITelegramBotClient>();
    private readonly FakeTimeProvider clock = new();

    [Fact]
    public async Task ExecuteAsync_啟動時getUpdates就失敗_應記錄後等待再重試而非讓服務結束()
    {
        bot.SendRequest(Arg.Any<IRequest<Update[]>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ApiRequestException("Unauthorized", 401));
        using var cts = new CancellationTokenSource();
        var service = Service();

        await service.StartAsync(cts.Token);
        WaitForCalls(1);
        AdvanceUntilCalls(2, TimeSpan.FromSeconds(10));
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        service.ExecuteTask!.IsFaulted.Should().BeFalse();
        CallCount().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExecuteAsync_polling中持續失敗_應每五秒才重試一次()
    {
        var calls = 0;
        bot.SendRequest(Arg.Any<IRequest<Update[]>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                // 第一次是 DropPendingUpdates 的呼叫，讓它成功；之後的 polling 一律失敗。
                calls++;
                return calls == 1 ? Task.FromResult(Array.Empty<Update>()) : throw new RequestException("Network is unreachable");
            });
        using var cts = new CancellationTokenSource();
        var service = Service();

        await service.StartAsync(cts.Token);
        WaitForCalls(2);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        var beforeAdvance = CallCount();
        AdvanceUntilCalls(beforeAdvance + 1, TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        beforeAdvance.Should().Be(2);
        service.ExecuteTask!.IsFaulted.Should().BeFalse();
    }

    private TelegramPollingService Service() =>
        new(bot, Handler(), clock, NullLogger<TelegramPollingService>.Instance);

    /// <summary>這些測試不會收到任何訊息，handler 只要能建構就好。</summary>
    private static MemeBotHandler Handler()
    {
        var repository = Substitute.For<IMemeRepository>();
        return new MemeBotHandler(
            repository,
            new MemeSearch(repository),
            Substitute.For<IImageStore>(),
            Substitute.For<IBotMessenger>(),
            Options.Create(new TelegramOptions { BotToken = "x", AllowedUserIds = [1] }),
            Options.Create(new SearchOptions()),
            NullLogger<MemeBotHandler>.Instance);
    }

    private int CallCount() => bot.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ITelegramBotClient.SendRequest));

    private void WaitForCalls(int count) =>
        SpinWait.SpinUntil(() => CallCount() >= count, WaitLimit).Should().BeTrue($"expected at least {count} SendRequest calls");

    /// <summary>
    /// 服務要等例外進到 catch 才會向假時鐘註冊計時器，單次 Advance 可能搶在它之前而永遠不觸發，
    /// 所以邊等邊推進：計時器晚建立也會被下一次推進叫醒。
    /// </summary>
    private void AdvanceUntilCalls(int count, TimeSpan step) =>
        SpinWait.SpinUntil(
            () =>
            {
                clock.Advance(step);
                return CallCount() >= count;
            },
            WaitLimit).Should().BeTrue($"expected at least {count} SendRequest calls after advancing the clock");
}
