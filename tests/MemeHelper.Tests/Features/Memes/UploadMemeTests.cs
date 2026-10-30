using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using MemeHelper.Api.Features.Memes;
using MemeHelper.Tests.Support;
using Microsoft.AspNetCore.Mvc;

namespace MemeHelper.Tests.Features.Memes;

public sealed class UploadMemeTests : IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly MemeApiClient client;

    public UploadMemeTests()
    {
        client = new MemeApiClient(factory.CreateClient());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task Upload_上傳png並設定關鍵字_應回傳201且圖片檔存在()
    {
        var response = await client.UploadAsync(SampleImages.Png(), "meme.png", "太棒了,章魚哥", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var meme = await response.Content.ReadFromJsonAsync<Meme>(Ct);
        meme.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be($"/api/memes/{meme!.Id}");
        meme.Keywords.Should().Equal("太棒了", "章魚哥");
        meme.IsEnabled.Should().BeTrue();
        File.Exists(Path.Combine(factory.ImageRoot, $"{meme.Id}.png")).Should().BeTrue();
    }

    [Fact]
    public async Task Upload_內容相同的圖片已存在_應回傳409並指出既有編號()
    {
        var existing = await client.UploadOkAsync(SampleImages.Png(), "meme.png", "六點", Ct);

        var response = await client.UploadAsync(SampleImages.Png(), "again.png", "下班", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        problem.Should().NotBeNull();
        problem!.Extensions["existingId"]!.ToString().Should().Be(existing.Id.ToString(CultureInfo.InvariantCulture));
        var all = await client.Http.GetFromJsonAsync<List<Meme>>("/api/memes", Ct);
        all.Should().HaveCount(1);
    }

    [Fact]
    public async Task Upload_關鍵字含空白與重複_應正規化()
    {
        var meme = await client.UploadOkAsync(SampleImages.Jpg(), "meme.jpg", " 下班 ,,下班,Hello,hello", Ct);

        meme.Keywords.Should().Equal("下班", "Hello");
    }

    [Fact]
    public async Task Upload_上傳gif_應回傳400()
    {
        var response = await client.UploadAsync(SampleImages.Gif(), "meme.gif", "六點", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_檔案超過10MB_應回傳400()
    {
        var tooBig = new byte[(10 * 1024 * 1024) + 1];
        SampleImages.Png().CopyTo(tooBig, 0);

        var response = await client.UploadAsync(tooBig, "meme.png", "六點", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_副檔名為png但檔頭不是png_應回傳400()
    {
        var response = await client.UploadAsync(SampleImages.Gif(), "meme.png", "六點", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_沒有有效關鍵字_應回傳400()
    {
        var response = await client.UploadAsync(SampleImages.Png(), "meme.png", " , ", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
