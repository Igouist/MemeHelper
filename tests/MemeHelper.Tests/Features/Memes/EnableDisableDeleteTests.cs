using System.Net;
using System.Net.Http.Json;
using MemeHelper.Api.Features.Memes;
using MemeHelper.Tests.Support;

namespace MemeHelper.Tests.Features.Memes;

public sealed class EnableDisableDeleteTests : IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly MemeApiClient client;

    public EnableDisableDeleteTests()
    {
        client = new MemeApiClient(factory.CreateClient());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task Disable_停用後_列表中該圖IsEnabled應為false且搜尋不到()
    {
        var meme = await client.UploadOkAsync(SampleImages.Png(), "a.png", "六點", Ct);

        var response = await client.Http.PostAsync($"/api/memes/{meme.Id}/disable", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<Meme>(Ct))!.IsEnabled.Should().BeFalse();
        var all = await client.Http.GetFromJsonAsync<List<Meme>>("/api/memes", Ct);
        all!.Single(m => m.Id == meme.Id).IsEnabled.Should().BeFalse();
        var enabledOnly = await client.Http.GetFromJsonAsync<List<Meme>>("/api/memes?keyword=六點&includeDisabled=false", Ct);
        enabledOnly.Should().BeEmpty();
    }

    [Fact]
    public async Task Enable_重新啟用_搜尋應找得到()
    {
        var meme = await client.UploadOkAsync(SampleImages.Png(), "a.png", "神奇海螺", Ct);
        await client.Http.PostAsync($"/api/memes/{meme.Id}/disable", null, Ct);

        var response = await client.Http.PostAsync($"/api/memes/{meme.Id}/enable", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var found = await client.Http.GetFromJsonAsync<List<Meme>>("/api/memes?keyword=海螺&includeDisabled=false", Ct);
        found!.Select(m => m.Id).Should().Equal(meme.Id);
    }

    [Fact]
    public async Task Delete_刪除存在的圖_應回傳204且圖片檔與紀錄都消失()
    {
        var meme = await client.UploadOkAsync(SampleImages.Png(), "a.png", "六點", Ct);
        var imagePath = Path.Combine(factory.ImageRoot, $"{meme.Id}.png");
        File.Exists(imagePath).Should().BeTrue();

        var response = await client.Http.DeleteAsync($"/api/memes/{meme.Id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        File.Exists(imagePath).Should().BeFalse();
        (await client.Http.GetAsync($"/api/memes/{meme.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Disable_編號不存在_應回傳404()
    {
        var response = await client.Http.PostAsync("/api/memes/999/disable", null, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
