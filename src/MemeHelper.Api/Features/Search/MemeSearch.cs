using MemeHelper.Api.Database;
using MemeHelper.Api.Features.Memes;

namespace MemeHelper.Api.Features.Search;

/// <summary>
/// 整批讀出再在記憶體比對。數千張圖、每張幾個關鍵字，整批讀出比寫 LIKE 加跳脫簡單，
/// 規則也能用純函式測試。
/// </summary>
public sealed class MemeSearch(IMemeRepository repository)
{
    public async Task<IReadOnlyList<Meme>> FindAsync(string query, bool includeDisabled, CancellationToken ct)
    {
        var terms = KeywordMatcher.ParseTerms(query);
        var memes = await repository.ListAsync(includeDisabled, ct);
        return memes.Where(m => KeywordMatcher.Matches(m.Keywords, terms)).ToList();
    }
}
