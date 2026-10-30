using MemeHelper.Api.Telegram;
using Microsoft.Extensions.Time.Testing;

namespace MemeHelper.Tests.Telegram;

public class PollingHealthTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LastSuccessAt_還沒成功過_應為null()
    {
        new PollingHealth(new FakeTimeProvider(Now)).LastSuccessAt.Should().BeNull();
    }

    [Fact]
    public void MarkSuccess_呼叫兩次_應記最後一次的時間()
    {
        var clock = new FakeTimeProvider(Now);
        var health = new PollingHealth(clock);

        health.MarkSuccess();
        clock.Advance(TimeSpan.FromSeconds(90));
        health.MarkSuccess();

        health.LastSuccessAt.Should().Be(Now.AddSeconds(90));
    }
}
