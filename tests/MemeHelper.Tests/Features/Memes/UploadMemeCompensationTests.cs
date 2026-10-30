using System.Net;
using System.Net.Http.Json;
using MemeHelper.Api.Features.Memes;
using MemeHelper.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MemeHelper.Tests.Features.Memes;

/// <summary>存檔失敗時不能留下半筆紀錄或寫到一半的檔。用會失敗的 IImageStore 取代真的檔案系統。</summary>
public sealed class UploadMemeCompensationTests : IDisposable
{
    private readonly IImageStore images = Substitute.For<IImageStore>();
    private readonly ApiFactory factory;
    private readonly MemeApiClient client;

    public UploadMemeCompensationTests()
    {
        images.SaveAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("disk full"));
        factory = new ApiFactory
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IImageStore>();
                services.AddSingleton(images);
            },
        };
        client = new MemeApiClient(factory.CreateClient());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task Upload_存檔失敗_應回500且刪掉紀錄與寫到一半的檔()
    {
        var response = await client.UploadAsync(SampleImages.Png(), "meme.png", "六點", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        images.Received(1).Delete(Arg.Any<int>(), "png");
        var all = await client.Http.GetFromJsonAsync<List<Meme>>("/api/memes", Ct);
        all.Should().BeEmpty();
    }
}
