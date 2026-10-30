using System.Net;
using System.Text;
using MemeHelper.Api.Telegram;
using Microsoft.Extensions.Time.Testing;
using Telegram.Bot.Requests;

namespace MemeHelper.Tests.Telegram;

public class LongPollingBotClientTests
{
    private const string FakeToken = "123456789:AAFakeTokenForTests-0123456789abcdefg";
    private const string EmptyUpdates = """{"ok":true,"result":[]}""";

    private static readonly DateTimeOffset Now = new(2026, 10, 30, 1, 0, 0, TimeSpan.Zero);

    private readonly CapturingHandler handler = new();
    private readonly PollingHealth health = new(new FakeTimeProvider(Now));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendRequest_getUpdates_伺服器等待秒數應比HttpClient逾時短十秒()
    {
        await Bot().SendRequest(new GetUpdatesRequest { Timeout = 100 }, Ct);

        handler.LastBody.Should().Contain("\"timeout\":90");
    }

    [Fact]
    public async Task SendRequest_getUpdates為短輪詢_不應改動()
    {
        await Bot().SendRequest(new GetUpdatesRequest { Timeout = 0 }, Ct);

        handler.LastBody.Should().Contain("\"timeout\":0");
    }

    [Fact]
    public async Task SendRequest_getUpdates成功_應記錄成功時間()
    {
        await Bot().SendRequest(new GetUpdatesRequest { Timeout = 100 }, Ct);

        health.LastSuccessAt.Should().Be(Now);
    }

    [Fact]
    public async Task SendRequest_getUpdates失敗_不應記錄成功時間()
    {
        handler.Status = HttpStatusCode.Unauthorized;
        handler.Body = """{"ok":false,"error_code":401,"description":"Unauthorized"}""";

        var act = () => Bot().SendRequest(new GetUpdatesRequest { Timeout = 100 }, Ct);

        await act.Should().ThrowAsync<Exception>();
        health.LastSuccessAt.Should().BeNull();
    }

    [Fact]
    public async Task SendRequest_其他請求成功_不應記錄成功時間()
    {
        handler.Body = """{"ok":true,"result":{"id":1,"is_bot":true,"first_name":"bot"}}""";

        await Bot().SendRequest(new GetMeRequest(), Ct);

        health.LastSuccessAt.Should().BeNull();
    }

    private LongPollingBotClient Bot() =>
        new(FakeToken, health, new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(100) });

    /// <summary>預設回一個合法的空 getUpdates 結果，並記下送出的 JSON；狀態碼與內容可改。</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string LastBody { get; private set; } = "";

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public string Body { get; set; } = EmptyUpdates;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(Body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
