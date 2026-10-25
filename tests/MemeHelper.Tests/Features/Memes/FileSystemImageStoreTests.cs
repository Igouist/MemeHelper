using MemeHelper.Api.Configuration;
using MemeHelper.Api.Features.Memes;
using MemeHelper.Tests.Support;
using Microsoft.Extensions.Options;

namespace MemeHelper.Tests.Features.Memes;

public sealed class FileSystemImageStoreTests : IDisposable
{
    private readonly TempDirectory temp = new();
    private readonly string imageRoot;
    private readonly FileSystemImageStore store;

    public FileSystemImageStoreTests()
    {
        imageRoot = Path.Combine(temp.Path, "images");
        store = new FileSystemImageStore(Options.Create(new StorageOptions { ImageRoot = imageRoot }));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => temp.Dispose();

    [Fact]
    public async Task SaveAsync_目錄不存在_應建立目錄並寫入檔案()
    {
        var content = SampleImages.Png();

        await store.SaveAsync(7, "png", new MemoryStream(content), Ct);

        var path = Path.Combine(imageRoot, "7.png");
        File.Exists(path).Should().BeTrue();
        (await File.ReadAllBytesAsync(path, Ct)).Should().Equal(content);
    }

    [Fact]
    public async Task OpenRead_檔案存在_應回傳可讀取的串流()
    {
        var content = SampleImages.Jpg();
        await store.SaveAsync(3, "jpg", new MemoryStream(content), Ct);

        await using var stream = store.OpenRead(3, "jpg");

        stream.Should().NotBeNull();
        using var buffer = new MemoryStream();
        await stream!.CopyToAsync(buffer, Ct);
        buffer.ToArray().Should().Equal(content);
    }

    [Fact]
    public void OpenRead_檔案不存在_應回傳null()
    {
        store.OpenRead(99, "png").Should().BeNull();
    }

    [Fact]
    public async Task Delete_檔案存在_應刪除並回傳true()
    {
        await store.SaveAsync(5, "png", new MemoryStream(SampleImages.Png()), Ct);

        store.Delete(5, "png").Should().BeTrue();

        File.Exists(Path.Combine(imageRoot, "5.png")).Should().BeFalse();
    }

    [Fact]
    public void Delete_檔案不存在_應回傳false()
    {
        store.Delete(99, "png").Should().BeFalse();
    }
}
