using MemeHelper.Api.Features.Memes;

namespace MemeHelper.Api.Database;

public enum RemoveKeywordResult
{
    Removed,
    MemeNotFound,
    KeywordNotFound,
}

public interface IMemeRepository
{
    /// <summary>contentHash 有唯一索引，重複會拋出例外；呼叫端應先用 FindByContentHashAsync 查。</summary>
    Task<int> AddAsync(string extension, string contentHash, IReadOnlyList<string> keywords, CancellationToken ct);

    Task<Meme?> GetAsync(int id, CancellationToken ct);

    Task<Meme?> FindByContentHashAsync(string contentHash, CancellationToken ct);

    /// <summary>升級前新增、還沒有內容雜湊的梗圖，依編號由小到大。</summary>
    Task<IReadOnlyList<Meme>> ListWithoutContentHashAsync(CancellationToken ct);

    Task<bool> SetContentHashAsync(int id, string contentHash, CancellationToken ct);

    /// <summary>依編號由小到大。</summary>
    Task<IReadOnlyList<Meme>> ListAsync(bool includeDisabled, CancellationToken ct);

    /// <summary>已存在的關鍵字（不分大小寫）略過不報錯。回傳 false 表示編號不存在。</summary>
    Task<bool> AddKeywordsAsync(int id, IReadOnlyList<string> keywords, CancellationToken ct);

    Task<RemoveKeywordResult> RemoveKeywordAsync(int id, string keyword, CancellationToken ct);

    Task<bool> SetEnabledAsync(int id, bool isEnabled, CancellationToken ct);

    Task<bool> SetTelegramFileIdAsync(int id, string? fileId, CancellationToken ct);

    /// <summary>回傳被刪的紀錄，呼叫端用它決定要刪哪個圖片檔。</summary>
    Task<Meme?> DeleteAsync(int id, CancellationToken ct);
}
