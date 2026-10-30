using System.Net;
using System.Net.Http.Json;
using MemeHelper.Tests.Support;
using Microsoft.AspNetCore.Mvc;

namespace MemeHelper.Tests.Features.Memes;

/// <summary>其他錯誤都是 ProblemDetails，404 也該是，腳本才能用同一種方式讀錯誤。</summary>
public sealed class NotFoundProblemDetailsTests : IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly MemeApiClient client;

    public NotFoundProblemDetailsTests()
    {
        client = new MemeApiClient(factory.CreateClient());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task GetMeme_編號不存在_應回傳ProblemDetails格式的404()
    {
        var response = await client.Http.GetAsync("/api/memes/999", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        problem!.Status.Should().Be(404);
    }

    [Fact]
    public async Task GetMemeImage_編號不存在_應回傳ProblemDetails格式的404()
    {
        var response = await client.Http.GetAsync("/api/memes/999/image", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
