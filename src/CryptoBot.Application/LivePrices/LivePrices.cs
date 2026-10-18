using System.Collections.Concurrent;
using System.Diagnostics;
using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Futures;

namespace CryptoBot.Application.LivePrices;

public sealed class LivePriceOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Số lần lấy giá giữ lại trong bộ nhớ cho mỗi symbol (240 × 15s = 1 giờ).</summary>
    public int HistorySize { get; set; } = 240;

    /// <summary>
    /// Mặc định để trống: binder của .NET cộng thêm phần tử từ cấu hình vào danh sách mặc định
    /// (gây trùng symbol), nên giá trị lấy hoàn toàn từ appsettings.json.
    /// </summary>
    public List<string> Symbols { get; set; } = [];
}

/// <summary>
/// Một lần lấy giá. Nếu một nguồn lỗi thì giá của nguồn đó là null và lý do nằm trong Error;
/// lỗi không bị che bằng giá cũ.
/// </summary>
public sealed record LivePriceSample(
    string Symbol,
    DateTime TimeUtc,
    decimal? SpotPrice,
    decimal? MarkPrice,
    int? SpotLatencyMs,
    int? MarkLatencyMs,
    string? Error);

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

/// <summary>Lấy giá Spot và Mark song song cho một symbol rồi ghi vào lịch sử.</summary>
public sealed class LivePriceSampler(
    IMarketDataClient spot,
    IMarkPriceClient mark,
    LivePriceHistory history,
    IClock clock)
{
    public async Task<LivePriceSample> SampleAsync(string symbol, CancellationToken ct)
    {
        var time = clock.UtcNow;
        var spotTask = TimedAsync(() => spot.GetLastPriceAsync(symbol, ct));
        var markTask = TimedAsync(async () => (await mark.GetMarkPriceAsync(symbol, ct)).Price);
        var (spotPrice, spotMs, spotError) = await spotTask;
        var (markPrice, markMs, markError) = await markTask;

        var errors = new List<string>();
        if (spotError is not null) errors.Add($"Spot: {spotError}");
        if (markError is not null) errors.Add($"Mark: {markError}");

        var sample = new LivePriceSample(symbol, time, spotPrice, markPrice, spotMs, markMs,
            errors.Count == 0 ? null : string.Join(" | ", errors));
        history.Add(sample);
        return sample;
    }

    private static async Task<(decimal? Price, int? Ms, string? Error)> TimedAsync(Func<Task<decimal>> fetch)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var price = await fetch();
            return (price, (int)sw.ElapsedMilliseconds, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, null, ex.Message);
        }
    }
}
