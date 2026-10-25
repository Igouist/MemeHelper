using System.Net;
using System.Net.Http.Json;
using MemeHelper.Api.Database;
using MemeHelper.Api.Features.Memes;
using MemeHelper.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace MemeHelper.Tests.Features.Memes;

public sealed class ListMemesTests : IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly MemeApiClient client;

    public ListMemesTests()
    {
        client = new MemeApiClient(factory.CreateClient());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task List_預設_應包含停用的圖()
    {
        var first = await client.UploadOkAsync(SampleImages.Png(), "a.png", "六點", Ct);
        var second = await client.UploadOkAsync(SampleImages.Png(), "b.png", "海螺", Ct);
        await factory.Services.GetRequiredService<IMemeRepository>().SetEnabledAsync(second.Id, false, Ct);

        var memes = await client.Http.GetFromJsonAsync<List<Meme>>("/api/memes", Ct);

        memes.Should().NotBeNull();
        memes!.Select(m => m.Id).Should().Equal(first.Id, second.Id);
        memes!.Single(m => m.Id == second.Id).IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task List_帶keyword_應只回傳命中的圖()
    {
        await client.UploadOkAsync(SampleImages.Png(), "a.png", "六點,下班", Ct);
        var target = await client.UploadOkAsync(SampleImages.Png(), "b.png", "神奇海螺", Ct);

        var memes = await client.Http.GetFromJsonAsync<List<Meme>>("/api/memes?keyword=海螺", Ct);

        memes!.Select(m => m.Id).Should().Equal(target.Id);
    }

    [Fact]
    public async Task Get_編號不存在_應回傳404()
    {
        var response = await client.Http.GetAsync("/api/memes/999", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
