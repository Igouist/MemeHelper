using System.ComponentModel.DataAnnotations;

namespace MemeHelper.Api.Configuration;

public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    /// <summary>備份資料夾，應在另一顆硬碟或同步資料夾。留空就不備份。</summary>
    public string Directory { get; set; } = "";

    /// <summary>資料庫快照保留幾份。圖片只增不刪，不受這個值影響。</summary>
    [Range(1, int.MaxValue)]
    public int KeepCount { get; set; } = 14;
}
