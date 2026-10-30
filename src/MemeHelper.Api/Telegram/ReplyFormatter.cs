using System.Text;
using MemeHelper.Api.Features.Memes;

namespace MemeHelper.Api.Telegram;

public static class ReplyFormatter
{
    public const string NotFound = "找不到";
    public const string Help = "傳關鍵字找梗圖，多個詞用空白隔開；命中多張時會列清單，再傳編號取圖。\n"
        + "傳圖加說明文字可新增梗圖；/info 編號 看關鍵字，/add 編號 關鍵字 與 /del 編號 關鍵字 修改關鍵字。";
    public const string ImageMissing = "圖片檔不見了，請到後台檢查編號 {0}";
    public const string SendFailed = "送圖失敗，請稍後再試";
    public const string UploadNeedsKeywords = "請在圖片的說明文字填關鍵字，用逗號或頓號隔開";
    public const string UploadUnsupportedType = "只接受 jpg 或 png";
    public const string UploadTooLarge = "檔案超過 10 MB";
    public const string UploadBadContent = "檔案內容不是 {0}";
    public const string UploadFailed = "上傳失敗，請稍後再試";
    public const string InfoUsage = "用法：/info 編號";
    public const string AddUsage = "用法：/add 編號 關鍵字1,關鍵字2";
    public const string DelUsage = "用法：/del 編號 關鍵字1,關鍵字2";
    public const string MemeNotFound = "沒有編號 {0}";

    public static string FormatAdded(Meme meme) => $"已新增【{meme.Id}】{string.Join(",", meme.Keywords)}";

    public static string FormatExists(Meme meme) => "已存在" + FormatKeywords(meme);

    /// <summary>【編號】關鍵字清單，停用中加註；清單格式與 FormatList 的每一行相同。</summary>
    public static string FormatKeywords(Meme meme)
    {
        var keywords = meme.Keywords.Count == 0 ? "（沒有關鍵字）" : string.Join(",", meme.Keywords);
        return $"【{meme.Id}】{keywords}{(meme.IsEnabled ? "" : "（已停用）")}";
    }

    public static string FormatRemoved(Meme meme, IReadOnlyList<string> missing) =>
        missing.Count == 0 ? FormatKeywords(meme) : $"{FormatKeywords(meme)}\n沒有這些關鍵字：{string.Join(",", missing)}";

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
