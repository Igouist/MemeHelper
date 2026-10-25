namespace MemeHelper.Tests.Support;

/// <summary>
/// 只有正確檔頭的假圖片。程式只檢查 magic bytes，不解碼，所以不需要真的可顯示的圖。
/// </summary>
public static class SampleImages
{
    private static readonly byte[] Filler = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

    public static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. Filler];

    public static byte[] Jpg() => [0xFF, 0xD8, 0xFF, 0xE0, .. Filler];

    public static byte[] Gif() => [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, .. Filler];
}
