using MemeHelper.Api.Configuration;
using MemeHelper.Api.Database;
using MemeHelper.Api.Features.Memes;
using MemeHelper.Api.Features.Search;
using MemeHelper.Api.Telegram;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MemeHelper.Tests.Telegram;

public class MemeBotHandlerTests
{
    private const long Me = 1;
    private const long Stranger = 999;
    private const long Chat = 10;

    private static readonly DateTimeOffset Now = new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);

    private readonly List<Meme> library =
    [
        Meme(1, true, null, "六點", "下班", "蟹老闆"),
        Meme(2, true, null, "六點", "太棒了", "章魚哥"),
        Meme(3, true, null, "神奇海螺", "不行"),
        Meme(4, false, null, "六點", "已停用"),
        Meme(5, true, null, "Hello", "打招呼"),
        Meme(6, true, null, "2077"),
    ];

    private readonly IMemeRepository repository = Substitute.For<IMemeRepository>();
    private readonly IImageStore images = Substitute.For<IImageStore>();
    private readonly IBotMessenger messenger = Substitute.For<IBotMessenger>();

    public MemeBotHandlerTests()
    {
        repository.ListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => library.Where(m => call.Arg<bool>() || m.IsEnabled).ToList());
        repository.GetAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => library.SingleOrDefault(m => m.Id == call.Arg<int>()));
        repository.SetTelegramFileIdAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(true);
        images.OpenRead(Arg.Any<int>(), Arg.Any<string>()).Returns(_ => new MemoryStream([1, 2, 3]));
        messenger.SendPhotoFileAsync(Arg.Any<long>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("NEW_FILE_ID");
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleTextAsync_非白名單使用者_不應呼叫messenger()
    {
        await Handler().HandleTextAsync(Stranger, Chat, "六點", Ct);

        messenger.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task HandleTextAsync_傳送start_應回覆Help()
    {
        await Handler().HandleTextAsync(Me, Chat, "/start", Ct);

        await messenger.Received(1).SendTextAsync(Chat, ReplyFormatter.Help, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_關鍵字命中一張_應用檔案送圖並儲存file_id()
    {
        await Handler().HandleTextAsync(Me, Chat, "海螺", Ct);

        await messenger.Received(1).SendPhotoFileAsync(Chat, Arg.Any<Stream>(), "3.png", Arg.Any<CancellationToken>());
        await repository.Received(1).SetTelegramFileIdAsync(3, "NEW_FILE_ID", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_關鍵字命中多張_應回覆清單()
    {
        await Handler().HandleTextAsync(Me, Chat, "六點", Ct);

        await messenger.Received(1).SendTextAsync(Chat, "【1】六點,下班,蟹老闆\n【2】六點,太棒了,章魚哥", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_多詞查詢_每個詞都要命中()
    {
        await Handler().HandleTextAsync(Me, Chat, "六點 下班", Ct);

        await messenger.Received(1).SendPhotoFileAsync(Chat, Arg.Any<Stream>(), "1.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_停用的圖_不應出現在結果()
    {
        await Handler().HandleTextAsync(Me, Chat, "已停用", Ct);

        await messenger.Received(1).SendTextAsync(Chat, ReplyFormatter.NotFound, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_找不到_應回覆找不到()
    {
        await Handler().HandleTextAsync(Me, Chat, "不存在的詞", Ct);

        await messenger.Received(1).SendTextAsync(Chat, ReplyFormatter.NotFound, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_傳送編號_應送出該編號的圖()
    {
        await Handler().HandleTextAsync(Me, Chat, "2", Ct);

        await messenger.Received(1).SendPhotoFileAsync(Chat, Arg.Any<Stream>(), "2.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_純數字但查無編號_應改當關鍵字搜尋()
    {
        await Handler().HandleTextAsync(Me, Chat, "2077", Ct);

        await messenger.Received(1).SendPhotoFileAsync(Chat, Arg.Any<Stream>(), "6.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_已有file_id_應用file_id重送且不開檔()
    {
        library[2] = library[2] with { TelegramFileId = "CACHED" };

        await Handler().HandleTextAsync(Me, Chat, "3", Ct);

        await messenger.Received(1).SendPhotoByFileIdAsync(Chat, "CACHED", Arg.Any<CancellationToken>());
        images.DidNotReceive().OpenRead(Arg.Any<int>(), Arg.Any<string>());
    }

    [Fact]
    public async Task HandleTextAsync_file_id重送失敗_應改用檔案上傳並更新file_id()
    {
        library[2] = library[2] with { TelegramFileId = "STALE" };
        messenger.SendPhotoByFileIdAsync(Chat, "STALE", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Bad Request: wrong file identifier"));

        await Handler().HandleTextAsync(Me, Chat, "3", Ct);

        await messenger.Received(1).SendPhotoFileAsync(Chat, Arg.Any<Stream>(), "3.png", Arg.Any<CancellationToken>());
        await repository.Received(1).SetTelegramFileIdAsync(3, "NEW_FILE_ID", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_圖片檔不存在_應回覆ImageMissing()
    {
        images.OpenRead(3, "png").Returns((Stream?)null);

        await Handler().HandleTextAsync(Me, Chat, "3", Ct);

        await messenger.Received(1).SendTextAsync(Chat, string.Format(ReplyFormatter.ImageMissing, 3), Arg.Any<CancellationToken>());
        await messenger.DidNotReceive().SendPhotoFileAsync(Arg.Any<long>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_純空白_應回覆找不到()
    {
        await Handler().HandleTextAsync(Me, Chat, "   ", Ct);

        await messenger.Received(1).SendTextAsync(Chat, ReplyFormatter.NotFound, Arg.Any<CancellationToken>());
    }

    private MemeBotHandler Handler() => new(
        repository,
        new MemeSearch(repository),
        images,
        messenger,
        Options.Create(new TelegramOptions { BotToken = "x", AllowedUserIds = [Me] }),
        Options.Create(new SearchOptions { MaxListResults = 20 }),
        NullLogger<MemeBotHandler>.Instance);

    private static Meme Meme(int id, bool enabled, string? fileId, params string[] keywords) =>
        new(id, "png", enabled, fileId, keywords, Now, Now);
}
