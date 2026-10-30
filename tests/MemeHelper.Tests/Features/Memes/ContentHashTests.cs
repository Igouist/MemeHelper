using System.Text;
using MemeHelper.Api.Features.Memes;

namespace MemeHelper.Tests.Features.Memes;

public class ContentHashTests
{
    // SHA-256("abc") 的標準測試向量。
    private const string AbcHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void Compute_已知內容_應回小寫十六進位的SHA256()
    {
        ContentHash.Compute(Encoding.ASCII.GetBytes("abc")).Should().Be(AbcHash);
    }

    [Fact]
    public async Task ComputeAsync_從串流讀_應與Compute結果相同()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("abc"));

        var hash = await ContentHash.ComputeAsync(stream, TestContext.Current.CancellationToken);

        hash.Should().Be(AbcHash);
    }
}
