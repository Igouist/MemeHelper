using MemeHelper.Api.Configuration;
using MemeHelper.Api.Database;
using MemeHelper.Api.Features.Memes;
using MemeHelper.Api.Features.Search;
using MemeHelper.Api.Telegram;
using MemeHelper.Tests.Support;
using Microsoft.Extensions.Logging;
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
    private readonly RecordingLogger<MemeBotHandler> log = new();

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
        messenger.DownloadFileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(SampleImages.Jpg());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleTextAsync_非白名單使用者_不應呼叫messenger()
    {
        await Handler().HandleTextAsync(Stranger, Chat, "六點", Ct);

        messenger.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task HandleTextAsync_非白名單使用者_應記錄警告含使用者編號()
    {
        await Handler().HandleTextAsync(Stranger, Chat, "六點", Ct);

        log.Has(LogLevel.Warning, Stranger.ToString()).Should().BeTrue();
    }

    [Fact]
    public async Task HandleImageAsync_非白名單使用者_應記錄警告含使用者編號()
    {
        await Handler().HandleImageAsync(Stranger, Chat, "PHOTO_ID", null, isPhoto: true, "六點", Ct);

        log.Has(LogLevel.Warning, Stranger.ToString()).Should().BeTrue();
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
    public async Task HandleTextAsync_送圖成功但存file_id失敗_不應回送圖失敗只記警告()
    {
        repository.SetTelegramFileIdAsync(3, "NEW_FILE_ID", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database is locked"));

        await Handler().HandleTextAsync(Me, Chat, "3", Ct);

        await messenger.Received(1).SendPhotoFileAsync(Chat, Arg.Any<Stream>(), "3.png", Arg.Any<CancellationToken>());
        await messenger.DidNotReceive().SendTextAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        log.Has(LogLevel.Warning, "3").Should().BeTrue();
    }

    [Fact]
    public async Task HandleTextAsync_送圖拋例外_應回SendFailed()
    {
        messenger.SendPhotoFileAsync(Chat, Arg.Any<Stream>(), "3.png", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("boom"));

        await Handler().HandleTextAsync(Me, Chat, "3", Ct);

        await messenger.Received(1).SendTextAsync(Chat, ReplyFormatter.SendFailed, Arg.Any<CancellationToken>());
        await repository.DidNotReceive().SetTelegramFileIdAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_傳送help_應回覆Help()
    {
        await Handler().HandleTextAsync(Me, Chat, "/help", Ct);

        await messenger.Received(1).SendTextAsync(Chat, ReplyFormatter.Help, Arg.Any<CancellationToken>());
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

    [Fact]
    public async Task HandleTextAsync_info指令_應回該編號目前的關鍵字()
    {
        await Handler().HandleTextAsync(Me, Chat, "/info 3", Ct);

        await messenger.Received(1).SendTextAsync(Chat, "【3】神奇海螺,不行", Arg.Any<CancellationToken>());
        await messenger.DidNotReceive().SendPhotoByFileIdAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await messenger.DidNotReceive().SendPhotoFileAsync(Arg.Any<long>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_info指令停用中的圖_應加註已停用()
    {
        await Handler().HandleTextAsync(Me, Chat, "/info 4", Ct);

        await messenger.Received(1).SendTextAsync(Chat, "【4】六點,已停用（已停用）", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_info指令編號不存在_應回沒有編號()
    {
        await Handler().HandleTextAsync(Me, Chat, "/info 99", Ct);

        await messenger.Received(1).SendTextAsync(Chat, "沒有編號 99", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_add指令_應新增關鍵字並回更新後的清單()
    {
        ArrangeKeywordEdits();

        await Handler().HandleTextAsync(Me, Chat, "/add 3 海螺、不行", Ct);

        await repository.Received(1).AddKeywordsAsync(3, Arg.Is<IReadOnlyList<string>>(k => k.SequenceEqual(new[] { "海螺", "不行" })), Arg.Any<CancellationToken>());
        await messenger.Received(1).SendTextAsync(Chat, "【3】神奇海螺,不行,海螺", Arg.Any<CancellationToken>());
        log.Has(LogLevel.Information, "3").Should().BeTrue();
    }

    [Fact]
    public async Task HandleTextAsync_add指令編號不存在_應回沒有編號()
    {
        ArrangeKeywordEdits();

        await Handler().HandleTextAsync(Me, Chat, "/add 99 海螺", Ct);

        await messenger.Received(1).SendTextAsync(Chat, "沒有編號 99", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_add指令停用中的圖_應加註已停用()
    {
        ArrangeKeywordEdits();

        await Handler().HandleTextAsync(Me, Chat, "/add 4 下班", Ct);

        await messenger.Received(1).SendTextAsync(Chat, "【4】六點,已停用,下班（已停用）", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_del指令_應移除關鍵字並回更新後的清單()
    {
        ArrangeKeywordEdits();

        await Handler().HandleTextAsync(Me, Chat, "/del 3 不行", Ct);

        await repository.Received(1).RemoveKeywordAsync(3, "不行", Arg.Any<CancellationToken>());
        await messenger.Received(1).SendTextAsync(Chat, "【3】神奇海螺", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_del指令有找不到的關鍵字_應移除找得到的並列出找不到的()
    {
        ArrangeKeywordEdits();

        await Handler().HandleTextAsync(Me, Chat, "/del 3 不行,下班", Ct);

        await messenger.Received(1).SendTextAsync(Chat, "【3】神奇海螺\n沒有這些關鍵字：下班", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_del指令移除最後一個關鍵字_應回沒有關鍵字()
    {
        ArrangeKeywordEdits();

        await Handler().HandleTextAsync(Me, Chat, "/del 3 神奇海螺,不行", Ct);

        await messenger.Received(1).SendTextAsync(Chat, "【3】（沒有關鍵字）", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_del指令編號不存在_應回沒有編號()
    {
        ArrangeKeywordEdits();

        await Handler().HandleTextAsync(Me, Chat, "/del 99 海螺", Ct);

        await messenger.Received(1).SendTextAsync(Chat, "沒有編號 99", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_指令格式不對_應回用法且不改資料()
    {
        await Handler().HandleTextAsync(Me, Chat, "/add 3", Ct);

        await messenger.Received(1).SendTextAsync(Chat, ReplyFormatter.AddUsage, Arg.Any<CancellationToken>());
        await repository.DidNotReceive().AddKeywordsAsync(Arg.Any<int>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleTextAsync_非白名單傳指令_不應呼叫messenger也不改資料()
    {
        ArrangeKeywordEdits();

        await Handler().HandleTextAsync(Stranger, Chat, "/add 3 海螺", Ct);

        messenger.ReceivedCalls().Should().BeEmpty();
        await repository.DidNotReceive().AddKeywordsAsync(Arg.Any<int>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_照片加caption_應新增並回覆編號且快取file_id()
    {
        ArrangeAdd("六點", "下班");

        await Handler().HandleImageAsync(Me, Chat, "PHOTO_ID", null, isPhoto: true, "六點,下班", Ct);

        await repository.Received(1).AddAsync("jpg", Arg.Any<string>(), Arg.Is<IReadOnlyList<string>>(k => k.SequenceEqual(new[] { "六點", "下班" })), Arg.Any<CancellationToken>());
        await images.Received(1).SaveAsync(7, "jpg", Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await repository.Received(1).SetTelegramFileIdAsync(7, "PHOTO_ID", Arg.Any<CancellationToken>());
        await messenger.Received(1).SendTextAsync(Chat, "已新增【7】六點,下班", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_檔案形式png_應以png新增且不快取file_id()
    {
        ArrangeAdd("神奇海螺");
        messenger.DownloadFileAsync("DOC_ID", Arg.Any<CancellationToken>()).Returns(SampleImages.Png());

        await Handler().HandleImageAsync(Me, Chat, "DOC_ID", "meme.png", isPhoto: false, "神奇海螺", Ct);

        await repository.Received(1).AddAsync("png", Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await images.Received(1).SaveAsync(7, "png", Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().SetTelegramFileIdAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await messenger.Received(1).SendTextAsync(Chat, "已新增【7】神奇海螺", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_caption經正規化_應去重去空白()
    {
        ArrangeAdd("下班", "Hello");

        await Handler().HandleImageAsync(Me, Chat, "PHOTO_ID", null, isPhoto: true, " 下班 ，，下班,Hello,hello", Ct);

        await repository.Received(1).AddAsync("jpg", Arg.Any<string>(), Arg.Is<IReadOnlyList<string>>(k => k.SequenceEqual(new[] { "下班", "Hello" })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_圖已存在_應回已存在與既有關鍵字且不新增()
    {
        ArrangeExisting(Meme(3, true, null, "神奇海螺", "不行"));

        await Handler().HandleImageAsync(Me, Chat, "PHOTO_ID", null, isPhoto: true, "海螺", Ct);

        await messenger.Received(1).SendTextAsync(Chat, "已存在【3】神奇海螺,不行", Arg.Any<CancellationToken>());
        await repository.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().AddKeywordsAsync(Arg.Any<int>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await images.DidNotReceive().SaveAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_圖已存在但停用_應加註已停用()
    {
        ArrangeExisting(Meme(8, false, null, "神奇海螺"));

        await Handler().HandleImageAsync(Me, Chat, "PHOTO_ID", null, isPhoto: true, "海螺", Ct);

        await messenger.Received(1).SendTextAsync(Chat, "已存在【8】神奇海螺（已停用）", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_圖已存在且沒有caption_應回已存在而非提示填關鍵字()
    {
        ArrangeExisting(Meme(3, true, null, "神奇海螺", "不行"));

        await Handler().HandleImageAsync(Me, Chat, "PHOTO_ID", null, isPhoto: true, null, Ct);

        await messenger.Received(1).SendTextAsync(Chat, "已存在【3】神奇海螺,不行", Arg.Any<CancellationToken>());
        await messenger.DidNotReceive().SendTextAsync(Chat, ReplyFormatter.UploadNeedsKeywords, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_沒有caption_應回提示且不新增()
    {
        await Handler().HandleImageAsync(Me, Chat, "PHOTO_ID", null, isPhoto: true, null, Ct);

        await messenger.Received(1).SendTextAsync(Chat, ReplyFormatter.UploadNeedsKeywords, Arg.Any<CancellationToken>());
        await repository.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_caption沒有有效關鍵字_應回提示且不新增()
    {
        await Handler().HandleImageAsync(Me, Chat, "PHOTO_ID", null, isPhoto: true, " , ", Ct);

        await messenger.Received(1).SendTextAsync(Chat, ReplyFormatter.UploadNeedsKeywords, Arg.Any<CancellationToken>());
        await repository.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_檔案形式非圖片_應回只接受jpg或png()
    {
        await Handler().HandleImageAsync(Me, Chat, "DOC_ID", "a.pdf", isPhoto: false, "六點", Ct);

        await messenger.Received(1).SendTextAsync(Chat, ReplyFormatter.UploadUnsupportedType, Arg.Any<CancellationToken>());
        await repository.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_檔案內容與副檔名不符_應回檔案內容不是png()
    {
        messenger.DownloadFileAsync("DOC_ID", Arg.Any<CancellationToken>()).Returns(SampleImages.Gif());

        await Handler().HandleImageAsync(Me, Chat, "DOC_ID", "a.png", isPhoto: false, "六點", Ct);

        await messenger.Received(1).SendTextAsync(Chat, string.Format(ReplyFormatter.UploadBadContent, "png"), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_檔案超過10MB_應回檔案超過10MB()
    {
        var tooBig = new byte[(10 * 1024 * 1024) + 1];
        SampleImages.Png().CopyTo(tooBig, 0);
        messenger.DownloadFileAsync("DOC_ID", Arg.Any<CancellationToken>()).Returns(tooBig);

        await Handler().HandleImageAsync(Me, Chat, "DOC_ID", "a.png", isPhoto: false, "六點", Ct);

        await messenger.Received(1).SendTextAsync(Chat, ReplyFormatter.UploadTooLarge, Arg.Any<CancellationToken>());
        await repository.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_下載失敗_應回上傳失敗且不新增()
    {
        messenger.DownloadFileAsync("PHOTO_ID", Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("boom"));

        await Handler().HandleImageAsync(Me, Chat, "PHOTO_ID", null, isPhoto: true, "六點", Ct);

        await messenger.Received(1).SendTextAsync(Chat, ReplyFormatter.UploadFailed, Arg.Any<CancellationToken>());
        await repository.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_存檔失敗_應刪除紀錄並回上傳失敗()
    {
        ArrangeAdd("六點");
        images.SaveAsync(7, "jpg", Arg.Any<Stream>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("disk full"));

        await Handler().HandleImageAsync(Me, Chat, "PHOTO_ID", null, isPhoto: true, "六點", Ct);

        await repository.Received(1).DeleteAsync(7, Arg.Any<CancellationToken>());
        await messenger.Received(1).SendTextAsync(Chat, ReplyFormatter.UploadFailed, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleImageAsync_非白名單_不應呼叫messenger()
    {
        await Handler().HandleImageAsync(Stranger, Chat, "PHOTO_ID", null, isPhoto: true, "六點", Ct);

        messenger.ReceivedCalls().Should().BeEmpty();
        await repository.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>讓 AddKeywordsAsync 與 RemoveKeywordAsync 真的改 library 裡的關鍵字，GetAsync 才讀得到結果。</summary>
    private void ArrangeKeywordEdits()
    {
        repository.AddKeywordsAsync(Arg.Any<int>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var index = library.FindIndex(m => m.Id == call.Arg<int>());
                if (index < 0)
                {
                    return false;
                }

                var fresh = call.Arg<IReadOnlyList<string>>().Where(k => !library[index].Keywords.Contains(k, StringComparer.OrdinalIgnoreCase));
                library[index] = library[index] with { Keywords = [.. library[index].Keywords, .. fresh] };
                return true;
            });
        repository.RemoveKeywordAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var index = library.FindIndex(m => m.Id == call.Arg<int>());
                if (index < 0)
                {
                    return RemoveKeywordResult.MemeNotFound;
                }

                var remaining = library[index].Keywords.Where(k => !k.Equals(call.Arg<string>(), StringComparison.OrdinalIgnoreCase)).ToList();
                if (remaining.Count == library[index].Keywords.Count)
                {
                    return RemoveKeywordResult.KeywordNotFound;
                }

                library[index] = library[index] with { Keywords = remaining };
                return RemoveKeywordResult.Removed;
            });
    }

    /// <summary>讓「下載到的預設 jpg」查重時命中這張。</summary>
    private void ArrangeExisting(Meme meme)
    {
        library.Add(meme);
        repository.FindByContentHashAsync(ContentHash.Compute(SampleImages.Jpg()), Arg.Any<CancellationToken>()).Returns(meme);
    }

    /// <summary>讓 AddAsync 回傳編號 7，且 GetAsync(7) 讀得到帶這些關鍵字的紀錄。</summary>
    private void ArrangeAdd(params string[] keywords)
    {
        repository.AddAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(7);
        library.Add(Meme(7, true, null, keywords));
    }

    private MemeBotHandler Handler() => new(
        repository,
        new MemeSearch(repository),
        images,
        messenger,
        Options.Create(new TelegramOptions { BotToken = "x", AllowedUserIds = [Me] }),
        Options.Create(new SearchOptions { MaxListResults = 20 }),
        log);

    [Fact]
    public async Task HandleTextAsync_查詢_應記錄查詢文字與命中數()
    {
        await Handler().HandleTextAsync(Me, Chat, "六點", Ct);

        log.Has(LogLevel.Information, "六點").Should().BeTrue();
        log.Has(LogLevel.Information, "2 張").Should().BeTrue();
    }

    [Fact]
    public async Task HandleTextAsync_以file_id回圖_應記錄編號與方式()
    {
        library[2] = library[2] with { TelegramFileId = "CACHED" };

        await Handler().HandleTextAsync(Me, Chat, "3", Ct);

        log.Has(LogLevel.Information, "file_id").Should().BeTrue();
        log.Entries.Should().Contain(e => e.Level == LogLevel.Information && e.Message.Contains("3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleImageAsync_新增成功_應記錄編號與關鍵字()
    {
        ArrangeAdd("六點", "下班");

        await Handler().HandleImageAsync(Me, Chat, "PHOTO_ID", null, isPhoto: true, "六點,下班", Ct);

        log.Has(LogLevel.Information, "7").Should().BeTrue();
        log.Has(LogLevel.Information, "六點,下班").Should().BeTrue();
    }

    private static Meme Meme(int id, bool enabled, string? fileId, params string[] keywords) =>
        new(id, "png", enabled, fileId, keywords, Now, Now);
}
