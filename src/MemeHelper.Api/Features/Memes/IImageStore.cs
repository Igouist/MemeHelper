namespace MemeHelper.Api.Features.Memes;

public interface IImageStore
{
    Task SaveAsync(int id, string extension, Stream content, CancellationToken ct);

    /// <summary>檔案不存在回 null，呼叫端決定怎麼回應使用者。</summary>
    Stream? OpenRead(int id, string extension);

    /// <summary>檔案不存在回 false。</summary>
    bool Delete(int id, string extension);
}
