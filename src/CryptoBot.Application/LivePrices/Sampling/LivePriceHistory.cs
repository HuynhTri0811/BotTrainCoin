using System.Collections.Concurrent;

namespace CryptoBot.Application.LivePrices;

/// <summary>Lịch sử các lần lấy giá trong bộ nhớ (chỉ để quan sát, không phải nguồn dữ liệu bền vững).</summary>
public sealed class LivePriceHistory(LivePriceOptions options)
{
    private readonly ConcurrentDictionary<string, LinkedList<LivePriceSample>> _samples = new();

    public void Add(LivePriceSample sample)
    {
        var list = _samples.GetOrAdd(sample.Symbol, _ => new LinkedList<LivePriceSample>());
        lock (list)
        {
            list.AddLast(sample);
            while (list.Count > options.HistorySize)
                list.RemoveFirst();
        }
    }

    /// <summary>Tối đa <paramref name="limit"/> mẫu mới nhất, sắp tăng dần theo thời gian.</summary>
    public IReadOnlyList<LivePriceSample> Get(string symbol, int limit)
    {
        if (!_samples.TryGetValue(symbol, out var list))
            return [];
        lock (list)
            return list.TakeLast(Math.Max(limit, 0)).ToList();
    }
}
