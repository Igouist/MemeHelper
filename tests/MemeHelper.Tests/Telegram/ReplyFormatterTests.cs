using MemeHelper.Api.Features.Memes;
using MemeHelper.Api.Telegram;

namespace MemeHelper.Tests.Telegram;

public class ReplyFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FormatList_輸入兩張_應輸出兩行編號加關鍵字()
    {
        IReadOnlyList<Meme> memes =
        [
            Meme(1, "六點", "下班", "蟹老闆"),
            Meme(2, "六點", "太棒了", "章魚哥"),
        ];

        var text = ReplyFormatter.FormatList(memes, 20);

        text.Should().Be("【1】六點,下班,蟹老闆\n【2】六點,太棒了,章魚哥");
    }

    [Fact]
    public void FormatList_輸入25張上限20_應只有20行並附提示()
    {
        var memes = Enumerable.Range(1, 25).Select(i => Meme(i, "爆量")).ToList();

        var lines = ReplyFormatter.FormatList(memes, 20).Split('\n');

        lines.Should().HaveCount(21);
        lines[19].Should().Be("【20】爆量");
        lines[20].Should().Be("還有 5 張沒列出，請再加關鍵字縮小範圍");
    }

    [Fact]
    public void FormatExists_啟用中_應為已存在加編號與關鍵字()
    {
        ReplyFormatter.FormatExists(Meme(3, "神奇海螺", "不行")).Should().Be("已存在【3】神奇海螺,不行");
    }

    [Fact]
    public void FormatExists_停用中_應加註已停用()
    {
        var meme = Meme(3, "神奇海螺") with { IsEnabled = false };

        ReplyFormatter.FormatExists(meme).Should().Be("已存在【3】神奇海螺（已停用）");
    }

    [Fact]
    public void FormatKeywords_啟用中_應為編號加關鍵字()
    {
        ReplyFormatter.FormatKeywords(Meme(3, "神奇海螺", "不行")).Should().Be("【3】神奇海螺,不行");
    }

    [Fact]
    public void FormatKeywords_停用中_應加註已停用()
    {
        ReplyFormatter.FormatKeywords(Meme(4, "六點") with { IsEnabled = false }).Should().Be("【4】六點（已停用）");
    }

    [Fact]
    public void FormatKeywords_沒有關鍵字_應標示沒有關鍵字()
    {
        ReplyFormatter.FormatKeywords(Meme(3)).Should().Be("【3】（沒有關鍵字）");
    }

    [Fact]
    public void FormatRemoved_全部找得到_應只回關鍵字清單()
    {
        ReplyFormatter.FormatRemoved(Meme(3, "神奇海螺"), []).Should().Be("【3】神奇海螺");
    }

    [Fact]
    public void FormatRemoved_有找不到的_應在末尾加一行列出()
    {
        ReplyFormatter.FormatRemoved(Meme(3, "神奇海螺"), ["下班", "六點"]).Should().Be("【3】神奇海螺\n沒有這些關鍵字：下班,六點");
    }

    private static Meme Meme(int id, params string[] keywords) => new(id, "png", true, null, keywords, Now, Now);
}
