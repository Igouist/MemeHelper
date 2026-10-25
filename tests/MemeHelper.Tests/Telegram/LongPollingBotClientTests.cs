using System.Net;
using System.Text;
using MemeHelper.Api.Telegram;
using Telegram.Bot.Requests;

namespace MemeHelper.Tests.Telegram;

public class LongPollingBotClientTests
{
    private const string FakeToken = "123456789:AAFakeTokenForTests-0123456789abcdefg";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendRequest_getUpdates_伺服器等待秒數應比HttpClient逾時短十秒()
    {
        var handler = new CapturingHandler();
        var bot = new LongPollingBotClient(FakeToken, new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(100) });

        await bot.SendRequest(new GetUpdatesRequest { Timeout = 100 }, Ct);

        handler.LastBody.Should().Contain("\"timeout\":90");
    }

    [Fact]
    public async Task SendRequest_getUpdates為短輪詢_不應改動()
    {
        var handler = new CapturingHandler();
        var bot = new LongPollingBotClient(FakeToken, new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(100) });

        await bot.SendRequest(new GetUpdatesRequest { Timeout = 0 }, Ct);

        handler.LastBody.Should().Contain("\"timeout\":0");
    }

    /// <summary>回一個合法的空 getUpdates 結果，並記下送出的 JSON。</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string LastBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"result":[]}""", Encoding.UTF8, "application/json"),
            };
        }
    }
}
