using MemeHelper.Api.Telegram;

namespace MemeHelper.Tests.Telegram;

public class KeywordCommandTests
{
    [Fact]
    public void TryParse_add加編號加關鍵字_應為Add且關鍵字經正規化()
    {
        var command = KeywordCommand.TryParse("/add 4 下班、六點,下班");

        command.Should().BeOfType<KeywordCommand.Add>();
        var add = (KeywordCommand.Add)command!;
        add.MemeId.Should().Be(4);
        add.Keywords.Should().Equal("下班", "六點");
    }

    [Fact]
    public void TryParse_del_應為Remove()
    {
        var command = KeywordCommand.TryParse("/del 4 下班,不行");

        command.Should().BeOfType<KeywordCommand.Remove>();
        var remove = (KeywordCommand.Remove)command!;
        remove.MemeId.Should().Be(4);
        remove.Keywords.Should().Equal("下班", "不行");
    }

    [Fact]
    public void TryParse_info加編號_應為Show()
    {
        KeywordCommand.TryParse("/info 4").Should().Be(new KeywordCommand.Show(4));
    }

    [Theory]
    [InlineData("/info")]
    [InlineData("/info x")]
    [InlineData("/info 4 下班")]
    public void TryParse_info格式不對_應為Invalid並附用法(string text)
    {
        KeywordCommand.TryParse(text).Should().Be(new KeywordCommand.Invalid(ReplyFormatter.InfoUsage));
    }

    [Theory]
    [InlineData("六點 下班")]
    [InlineData("/help")]
    [InlineData("/added 4 下班")]
    [InlineData("4")]
    public void TryParse_不是指令_應為null(string text)
    {
        KeywordCommand.TryParse(text).Should().BeNull();
    }

    [Theory]
    [InlineData("/add")]
    [InlineData("/add 4")]
    [InlineData("/add x 下班")]
    [InlineData("/add 4 , ")]
    [InlineData("/add -1 下班")]
    public void TryParse_add格式不對_應為Invalid並附用法(string text)
    {
        KeywordCommand.TryParse(text).Should().Be(new KeywordCommand.Invalid(ReplyFormatter.AddUsage));
    }

    [Theory]
    [InlineData("/del")]
    [InlineData("/del 4")]
    public void TryParse_del格式不對_應為Invalid並附用法(string text)
    {
        KeywordCommand.TryParse(text).Should().Be(new KeywordCommand.Invalid(ReplyFormatter.DelUsage));
    }

    [Fact]
    public void TryParse_指令大小寫與多餘空白_應不影響()
    {
        var command = KeywordCommand.TryParse("  /ADD   4   下班  ");

        command.Should().BeOfType<KeywordCommand.Add>();
        ((KeywordCommand.Add)command!).Keywords.Should().Equal("下班");
    }
}
