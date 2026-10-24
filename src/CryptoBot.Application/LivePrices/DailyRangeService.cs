using System.Collections.Concurrent;
using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.LivePrices;

/// <summary>
/// Biên độ dự kiến 24 giờ cho một symbol: lấy nến ngày đã đóng từ sàn (cache vì chỉ đổi mỗi ngày một lần),
/// mốc giá là Spot mới nhất trong <see cref="LivePriceHistory"/>, không có thì dùng giá đóng gần nhất.
/// </summary>
public sealed class DailyRangeService(IMarketDataClient client, LivePriceHistory history, IClock clock)
{
    /// <summary>Số ngày lịch sử dùng để tính.</summary>
    public const int LookbackDays = 90;

    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, (DateTime FetchedAtUtc, IReadOnlyList<decimal> Closes)> _cache = new();

    public async Task<DailyMoveRange?> GetAsync(string symbol, CancellationToken ct)
    {
        var closes = await GetClosesAsync(symbol, ct);
        if (closes.Count == 0)
            return null;

        var spot = history.Get(symbol, 1).LastOrDefault()?.SpotPrice;
        return DailyMoveRange.Compute(closes, spot ?? closes[^1]);
    }

    private async Task<IReadOnlyList<decimal>> GetClosesAsync(string symbol, CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (_cache.TryGetValue(symbol, out var cached) && now - cached.FetchedAtUtc < CacheFor)
            return cached.Closes;

        var day = CandleInterval.D1.Duration;
        var today = now.Date;
        var klines = await client.GetKlinesAsync(symbol, CandleInterval.D1, today - day * (LookbackDays + 1),
            LookbackDays + 2, ct);
        // Bỏ ngày đang chạy: giá đóng của nó chưa chốt.
        var closes = klines.Where(k => k.OpenTimeUtc + day <= now).Select(k => k.Close).ToList();
        _cache[symbol] = (now, closes);
        return closes;
    }
}
