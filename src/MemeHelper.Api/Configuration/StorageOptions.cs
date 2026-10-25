namespace MemeHelper.Api.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>梗圖原檔的資料夾，檔名為 {編號}.{副檔名}。</summary>
    public string ImageRoot { get; set; } = "Data/images";
}
