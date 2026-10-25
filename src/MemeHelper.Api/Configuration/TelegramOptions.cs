using System.ComponentModel.DataAnnotations;

namespace MemeHelper.Api.Configuration;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    [Required]
    public string BotToken { get; set; } = "";

    /// <summary>只有這些 Telegram user id 的訊息會被處理，其他人一律不回應。</summary>
    [MinLength(1)]
    public long[] AllowedUserIds { get; set; } = [];
}
