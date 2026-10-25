namespace MemeHelper.Api.Features.Memes;

/// <summary>
/// 上傳檔案的規則。只看副檔名會讓改過名的非圖片混進來，等到 Telegram 送圖才失敗，
/// 所以另外檢查檔頭 magic bytes；不解碼圖片，省掉影像套件。
/// </summary>
public static class ImageFileRules
{
    /// <summary>Telegram sendPhoto 的上限。</summary>
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpgSignature = [0xFF, 0xD8, 0xFF];

    public static bool TryGetExtension(string fileName, out string extension)
    {
        extension = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "jpg",
            ".png" => "png",
            _ => "",
        };
        return extension.Length > 0;
    }

    public static bool HasValidSignature(ReadOnlySpan<byte> head, string extension)
    {
        var signature = extension switch
        {
            "png" => PngSignature,
            "jpg" => JpgSignature,
            _ => null,
        };
        return signature is not null && head.StartsWith(signature);
    }

    public static string GetContentType(string extension) => extension switch
    {
        "jpg" => "image/jpeg",
        "png" => "image/png",
        _ => "application/octet-stream",
    };
}
