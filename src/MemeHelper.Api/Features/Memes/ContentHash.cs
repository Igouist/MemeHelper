using System.Security.Cryptography;

namespace MemeHelper.Api.Features.Memes;

/// <summary>圖片內容的 SHA-256，小寫十六進位，用來查重。</summary>
public static class ContentHash
{
    public static string Compute(ReadOnlySpan<byte> content) => Convert.ToHexStringLower(SHA256.HashData(content));

    public static async Task<string> ComputeAsync(Stream content, CancellationToken ct) =>
        Convert.ToHexStringLower(await SHA256.HashDataAsync(content, ct));
}
