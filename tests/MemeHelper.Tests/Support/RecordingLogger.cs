using Microsoft.Extensions.Logging;

namespace MemeHelper.Tests.Support;

/// <summary>
/// 把 log 收進清單，讓測試能斷言「有記到什麼」。背景服務會從另一條執行緒寫入，
/// 所以讀寫都上鎖，讀取時回傳複本。
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly Lock gate = new();
    private readonly List<(LogLevel Level, string Message)> entries = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (gate)
            {
                return [.. entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        lock (gate)
        {
            entries.Add((logLevel, message));
        }
    }

    public bool Has(LogLevel level, string fragment) =>
        Entries.Any(e => e.Level == level && e.Message.Contains(fragment, StringComparison.Ordinal));
}
