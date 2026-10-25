namespace MemeHelper.Api.Configuration;

public sealed class SearchOptions
{
    public const string SectionName = "Search";

    /// <summary>命中多張時清單最多列幾張，超過就提示再加詞縮小範圍。</summary>
    public int MaxListResults { get; set; } = 20;
}
