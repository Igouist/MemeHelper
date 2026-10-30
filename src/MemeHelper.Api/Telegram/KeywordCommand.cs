using System.Globalization;
using MemeHelper.Api.Features.Memes;

namespace MemeHelper.Api.Telegram;

/// <summary>
/// 手機上看與修關鍵字的指令：<c>/info 編號</c>、<c>/add 編號 關鍵字1,關鍵字2</c>、<c>/del 編號 關鍵字1,關鍵字2</c>。
/// 關鍵字的分隔與正規化和 caption 相同。純函式，不是指令回 null，格式不對回 Invalid 帶用法。
/// </summary>
public abstract record KeywordCommand
{
    public sealed record Show(int MemeId) : KeywordCommand;

    public sealed record Add(int MemeId, IReadOnlyList<string> Keywords) : KeywordCommand;

    public sealed record Remove(int MemeId, IReadOnlyList<string> Keywords) : KeywordCommand;

    public sealed record Invalid(string Usage) : KeywordCommand;

    public static KeywordCommand? TryParse(string text)
    {
        var parts = text.Split((char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var usage = parts[0].ToLowerInvariant() switch
        {
            "/info" => ReplyFormatter.InfoUsage,
            "/add" => ReplyFormatter.AddUsage,
            "/del" => ReplyFormatter.DelUsage,
            _ => null,
        };
        if (usage is null)
        {
            return null;
        }

        if (parts.Length < 2 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var memeId))
        {
            return new Invalid(usage);
        }

        if (usage == ReplyFormatter.InfoUsage)
        {
            return parts.Length == 2 ? new Show(memeId) : new Invalid(usage);
        }

        var keywords = parts.Length == 3 ? KeywordNormalizer.Normalize([parts[2]]) : [];
        if (keywords.Count == 0)
        {
            return new Invalid(usage);
        }

        return usage == ReplyFormatter.AddUsage ? new Add(memeId, keywords) : new Remove(memeId, keywords);
    }
}
