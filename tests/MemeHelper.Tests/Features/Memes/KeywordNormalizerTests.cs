using MemeHelper.Api.Features.Memes;

namespace MemeHelper.Tests.Features.Memes;

public class KeywordNormalizerTests
{
    [Fact]
    public void Normalize_輸入含頭尾空白_應去除空白()
    {
        KeywordNormalizer.Normalize([" 下班 "]).Should().Equal("下班");
    }

    [Fact]
    public void Normalize_輸入空字串與純空白_應忽略()
    {
        KeywordNormalizer.Normalize(["", "  ", "六點"]).Should().Equal("六點");
    }

    [Fact]
    public void Normalize_輸入大小寫不同的重複英文_應只保留第一個()
    {
        KeywordNormalizer.Normalize(["Hello", "hello"]).Should().Equal("Hello");
    }

    [Fact]
    public void Normalize_輸入中文重複_應只保留一個()
    {
        KeywordNormalizer.Normalize(["下班", "下班"]).Should().Equal("下班");
    }

    [Fact]
    public void Normalize_單一字串含逗號_應切成多個關鍵字()
    {
        KeywordNormalizer.Normalize(["六點,下班"]).Should().Equal("六點", "下班");
    }

    [Fact]
    public void Normalize_單一字串含全形逗號_應切成多個關鍵字()
    {
        KeywordNormalizer.Normalize(["六點，下班"]).Should().Equal("六點", "下班");
    }

    [Fact]
    public void Normalize_混合來源_應保留首次出現順序()
    {
        KeywordNormalizer.Normalize([" 下班 ,,下班,Hello,hello"]).Should().Equal("下班", "Hello");
    }
}
