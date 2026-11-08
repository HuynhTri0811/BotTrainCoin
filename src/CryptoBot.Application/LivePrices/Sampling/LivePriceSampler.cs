using System.Diagnostics;
using CryptoBot.Application.Abstractions;

namespace CryptoBot.Application.LivePrices;

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
