namespace MemeHelper.Api.Features.Memes;

/// <summary>
/// 所有關鍵字的入口（上傳、新增）都先經過這裡，規則只有一份。
/// 先以逗號切開是因為清單顯示用逗號連接關鍵字，含逗號的關鍵字會讓清單讀不出邊界。
/// </summary>
public static class KeywordNormalizer
{
    public static IReadOnlyList<string> Normalize(IEnumerable<string> raw)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var piece in raw.SelectMany(r => r.Split(',')))
        {
            var keyword = piece.Trim();
            if (keyword.Length > 0 && seen.Add(keyword))
            {
                result.Add(keyword);
            }
        }

        return result;
    }
}
