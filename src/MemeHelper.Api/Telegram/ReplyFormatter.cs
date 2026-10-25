using System.Text;
using MemeHelper.Api.Features.Memes;

namespace MemeHelper.Api.Telegram;

public static class ReplyFormatter
{
    public const string NotFound = "找不到";
    public const string Help = "傳關鍵字找梗圖，多個詞用空白隔開；命中多張時會列清單，再傳編號取圖。";
    public const string ImageMissing = "圖片檔不見了，請到後台檢查編號 {0}";
    public const string SendFailed = "送圖失敗，請稍後再試";

    public static string FormatList(IReadOnlyList<Meme> memes, int maxListResults)
    {
        var lines = memes.Take(maxListResults).Select(m => $"【{m.Id}】{string.Join(",", m.Keywords)}");
        var text = new StringBuilder(string.Join("\n", lines));
        if (memes.Count > maxListResults)
        {
            text.Append('\n').Append($"還有 {memes.Count - maxListResults} 張沒列出，請再加關鍵字縮小範圍");
        }

        return text.ToString();
    }
}
