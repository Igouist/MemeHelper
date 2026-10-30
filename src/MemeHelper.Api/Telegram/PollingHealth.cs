namespace MemeHelper.Api.Telegram;

/// <summary>
/// 最後一次 getUpdates 成功的時間，給 /api/status 用。閒置時 Telegram 每個等待窗口結束也算成功一次，
/// 所以這個時間超過幾分鐘沒動就是 polling 斷了。
/// </summary>
public sealed class PollingHealth(TimeProvider clock)
{
    // polling 執行緒寫、HTTP 執行緒讀。DateTimeOffset 不是原子寫入，裝箱成物件後換參考就是了。
    private object? lastSuccessAt;

    public DateTimeOffset? LastSuccessAt => (DateTimeOffset?)Volatile.Read(ref lastSuccessAt);

    public void MarkSuccess() => Volatile.Write(ref lastSuccessAt, clock.GetUtcNow());
}
