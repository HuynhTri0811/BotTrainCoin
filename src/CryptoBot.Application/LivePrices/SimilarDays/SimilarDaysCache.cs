namespace CryptoBot.Application.LivePrices;

/// <summary>
/// Dự báo và chạy thử chỉ đổi khi có nến ngày mới, nên mỗi (symbol, mốc) giữ đúng một kết quả theo ngày cuối:
/// có ngày mới thì thay, không phình theo thời gian. Lazy đảm bảo nhiều request cùng lúc chỉ tính một lần.
/// </summary>
public sealed class SimilarDaysCache
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Symbol, int Horizon), Entry> _entries = new();

    private sealed record Entry(DateTime LastDay, Lazy<Task<SimilarDaysComputation>> Value);

    public async Task<SimilarDaysComputation> GetOrComputeAsync(string symbol, int horizon, DateTime lastDay,
        Func<Task<SimilarDaysComputation>> compute)
    {
        Entry entry;
        lock (_gate)
        {
            if (!_entries.TryGetValue((symbol, horizon), out entry!) || entry.LastDay != lastDay)
                _entries[(symbol, horizon)] = entry =
                    new Entry(lastDay, new Lazy<Task<SimilarDaysComputation>>(compute));
        }

        try
        {
            return await entry.Value.Value;
        }
        catch
        {
            // Không giữ kết quả lỗi: lần gọi sau tính lại.
            lock (_gate)
            {
                if (_entries.TryGetValue((symbol, horizon), out var current) && ReferenceEquals(current, entry))
                    _entries.Remove((symbol, horizon));
            }

            throw;
        }
    }

    /// <summary>Số mục đang giữ (dùng cho test).</summary>
    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }
}
