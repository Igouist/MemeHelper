using System.Net;
using System.Net.Http.Json;
using MemeHelper.Api.Features.Memes;
using MemeHelper.Tests.Support;

namespace MemeHelper.Tests.Features.Memes;

public sealed class KeywordEndpointsTests : IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly MemeApiClient client;

    public KeywordEndpointsTests()
    {
        client = new MemeApiClient(factory.CreateClient());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task AddKeywords_新增一個關鍵字_應回傳200且關鍵字附加在最後()
    {
        var meme = await client.UploadOkAsync(SampleImages.Png(), "a.png", "六點,下班,蟹老闆", Ct);

        var response = await client.Http.PostAsJsonAsync($"/api/memes/{meme.Id}/keywords", new[] { "準時" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<Meme>(Ct);
        updated!.Keywords.Should().Equal("六點", "下班", "蟹老闆", "準時");
    }

    [Fact]
    public async Task AddKeywords_新增已存在的關鍵字_應回傳200且不重複()
    {
        var meme = await client.UploadOkAsync(SampleImages.Png(), "a.png", "六點,下班,蟹老闆", Ct);

        var response = await client.Http.PostAsJsonAsync($"/api/memes/{meme.Id}/keywords", new[] { "六點" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<Meme>(Ct);
        updated!.Keywords.Should().Equal("六點", "下班", "蟹老闆");
    }

    [Fact]
    public async Task AddKeywords_body全是空字串_應回傳400()
    {
        var meme = await client.UploadOkAsync(SampleImages.Png(), "a.png", "六點", Ct);

        var response = await client.Http.PostAsJsonAsync($"/api/memes/{meme.Id}/keywords", new[] { "", " " }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveKeyword_移除中文關鍵字_應回傳204且清單不含該字()
    {
        var meme = await client.UploadOkAsync(SampleImages.Png(), "a.png", "六點,下班,蟹老闆", Ct);

        var response = await client.Http.DeleteAsync($"/api/memes/{meme.Id}/keywords/{Uri.EscapeDataString("下班")}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var updated = await client.Http.GetFromJsonAsync<Meme>($"/api/memes/{meme.Id}", Ct);
        updated!.Keywords.Should().Equal("六點", "蟹老闆");
    }

    [Fact]
    public async Task RemoveKeyword_關鍵字不存在_應回傳404()
    {
        var meme = await client.UploadOkAsync(SampleImages.Png(), "a.png", "六點", Ct);

        var response = await client.Http.DeleteAsync($"/api/memes/{meme.Id}/keywords/{Uri.EscapeDataString("不存在")}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddKeywords_編號不存在_應回傳404()
    {
        var response = await client.Http.PostAsJsonAsync("/api/memes/999/keywords", new[] { "六點" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
