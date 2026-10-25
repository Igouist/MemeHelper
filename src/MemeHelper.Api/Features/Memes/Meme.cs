namespace MemeHelper.Api.Features.Memes;

/// <summary>
/// TelegramFileId 是第一次送到 Telegram 後記下的 file_id，之後重送直接用它，不再上傳檔案。
/// </summary>
public sealed record Meme(
    int Id,
    string Extension,
    bool IsEnabled,
    string? TelegramFileId,
    IReadOnlyList<string> Keywords,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
