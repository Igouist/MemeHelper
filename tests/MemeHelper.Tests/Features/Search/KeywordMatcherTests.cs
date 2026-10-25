using MemeHelper.Api.Features.Search;

namespace MemeHelper.Tests.Features.Search;

public class KeywordMatcherTests
{
    [Fact]
    public void ParseTerms_輸入多個空白分隔的詞_應切成多個詞()
    {
        KeywordMatcher.ParseTerms("六點  下班").Should().Equal("六點", "下班");
    }

    [Fact]
    public void ParseTerms_輸入純空白_應回傳空集合()
    {
        KeywordMatcher.ParseTerms("   ").Should().BeEmpty();
    }

    [Fact]
    public void Matches_單一詞為關鍵字的子字串_應回傳true()
    {
        KeywordMatcher.Matches(["蟹老闆"], ["老闆"]).Should().BeTrue();
    }

    [Fact]
    public void Matches_英文大小寫不同_應回傳true()
    {
        KeywordMatcher.Matches(["Hello"], ["hello"]).Should().BeTrue();
    }

    [Fact]
    public void Matches_多個詞全部命中_應回傳true()
    {
        KeywordMatcher.Matches(["六點", "下班"], ["六點", "下班"]).Should().BeTrue();
    }

    [Fact]
    public void Matches_多個詞只命中一個_應回傳false()
    {
        KeywordMatcher.Matches(["六點", "太棒了"], ["六點", "下班"]).Should().BeFalse();
    }

    [Fact]
    public void Matches_詞不在任何關鍵字內_應回傳false()
    {
        KeywordMatcher.Matches(["六點", "下班"], ["海螺"]).Should().BeFalse();
    }
}
