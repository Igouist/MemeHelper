namespace MemeHelper.Api.Features.Search;

/// <summary>
/// 比對規則只有這一份，Telegram 與管理 API 共用。純子字串比對、不分詞；
/// 英文不分大小寫靠 OrdinalIgnoreCase，中文本來就沒有大小寫。
/// </summary>
public static class KeywordMatcher
{
    public static IReadOnlyList<string> ParseTerms(string query) =>
        query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>每個詞都要有任一關鍵字包含它才算命中；沒有詞視為全部命中。</summary>
    public static bool Matches(IReadOnlyList<string> keywords, IReadOnlyList<string> terms) =>
        terms.All(term => keywords.Any(keyword => keyword.Contains(term, StringComparison.OrdinalIgnoreCase)));
}
