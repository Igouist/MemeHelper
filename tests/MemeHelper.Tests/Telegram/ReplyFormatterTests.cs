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

    private static Meme Meme(int id, params string[] keywords) => new(id, "png", true, null, keywords, Now, Now);
}
