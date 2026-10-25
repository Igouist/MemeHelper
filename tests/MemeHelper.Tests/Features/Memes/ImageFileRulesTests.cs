using MemeHelper.Api.Features.Memes;
using MemeHelper.Tests.Support;

namespace MemeHelper.Tests.Features.Memes;

public class ImageFileRulesTests
{
    [Theory]
    [InlineData("a.jpg", "jpg")]
    [InlineData("a.JPEG", "jpg")]
    [InlineData("a.PNG", "png")]
    public void TryGetExtension_輸入支援的副檔名_應回傳正規化副檔名(string fileName, string expected)
    {
        ImageFileRules.TryGetExtension(fileName, out var extension).Should().BeTrue();
        extension.Should().Be(expected);
    }

    [Theory]
    [InlineData("a.gif")]
    [InlineData("a")]
    [InlineData("a.webp")]
    public void TryGetExtension_輸入不支援的副檔名_應回傳false(string fileName)
    {
        ImageFileRules.TryGetExtension(fileName, out _).Should().BeFalse();
    }

    [Fact]
    public void HasValidSignature_png檔頭配png_應回傳true()
    {
        ImageFileRules.HasValidSignature(SampleImages.Png(), "png").Should().BeTrue();
    }

    [Fact]
    public void HasValidSignature_gif檔頭配png_應回傳false()
    {
        ImageFileRules.HasValidSignature(SampleImages.Gif(), "png").Should().BeFalse();
    }

    [Fact]
    public void HasValidSignature_jpg檔頭配jpg_應回傳true()
    {
        ImageFileRules.HasValidSignature(SampleImages.Jpg(), "jpg").Should().BeTrue();
    }

    [Fact]
    public void GetContentType_輸入jpg_應回傳image_jpeg()
    {
        ImageFileRules.GetContentType("jpg").Should().Be("image/jpeg");
    }
}
