using System.Net;
using MemeHelper.Tests.Support;

namespace MemeHelper.Tests.Features.Memes;

public sealed class GetMemeImageTests : IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly MemeApiClient client;

    public GetMemeImageTests()
    {
        client = new MemeApiClient(factory.CreateClient());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task GetImage_圖片存在_應回傳image_png與原始內容()
    {
        var content = SampleImages.Png();
        var meme = await client.UploadOkAsync(content, "a.png", "六點", Ct);

        var response = await client.Http.GetAsync($"/api/memes/{meme.Id}/image", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await response.Content.ReadAsByteArrayAsync(Ct)).Should().Equal(content);
    }

    [Fact]
    public async Task GetImage_紀錄存在但檔案被刪_應回傳404()
    {
        var meme = await client.UploadOkAsync(SampleImages.Png(), "a.png", "六點", Ct);
        File.Delete(Path.Combine(factory.ImageRoot, $"{meme.Id}.png"));

        var response = await client.Http.GetAsync($"/api/memes/{meme.Id}/image", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
