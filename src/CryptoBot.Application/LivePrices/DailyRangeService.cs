using System.Collections.Concurrent;
using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.LivePrices;

/// <summary>
/// Nến ngày đã đóng lấy thẳng từ sàn (không lưu DB, cache vì chỉ đổi mỗi ngày một lần), dùng cho
/// biên độ dự kiến 24 giờ và dự đoán theo ngày. Mốc giá của biên độ là Spot mới nhất trong
/// <see cref="LivePriceHistory"/>, không có thì dùng giá đóng gần nhất.
/// </summary>
public sealed class DailyRangeService(IMarketDataClient client, LivePriceHistory history, IClock clock)
{
    /// <summary>Số ngày lịch sử dùng để tính biên độ 24 giờ; mốc dài hơn dùng cả <see cref="FetchDays"/> ngày.</summary>
    public const int LookbackDays = 90;

    /// <summary>Mốc dài nhất được ước lượng.</summary>
    public const int MaxHorizonDays = 90;

    /// <summary>Số nến ngày tải về (một năm), đủ cho cả chạy thử dự đoán theo ngày.</summary>
    public const int FetchDays = 365;

    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, (DateTime FetchedAtUtc, IReadOnlyList<Candle> Candles)> _cache = new();

    /// <summary>Biên độ sau <paramref name="horizonDays"/> ngày (1 = 24 giờ).</summary>
    public async Task<DailyMoveRange?> GetAsync(string symbol, int horizonDays, CancellationToken ct)
    {
        var candles = await GetClosedDailyCandlesAsync(symbol, ct);
        if (candles.Count == 0)
            return null;

        // 24 giờ: 90 ngày gần nhất phản ánh biến động hiện tại. Mốc dài cần nhiều đoạn lịch sử hơn: dùng cả năm.
        var lookback = horizonDays == 1 ? LookbackDays : FetchDays;
        var closes = candles.TakeLast(lookback + 1).Select(c => c.Close).ToList();
        var spot = history.Get(symbol, 1).LastOrDefault()?.SpotPrice;
        return DailyMoveRange.Compute(closes, spot ?? closes[^1], horizonDays);
    }

    /// <summary>Tối đa <see cref="FetchDays"/> nến ngày đã đóng gần nhất, tăng dần theo thời gian.</summary>
    public async Task<IReadOnlyList<Candle>> GetClosedDailyCandlesAsync(string symbol, CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (_cache.TryGetValue(symbol, out var cached) && now - cached.FetchedAtUtc < CacheFor)
            return cached.Candles;

        var day = CandleInterval.D1.Duration;
        var klines = await client.GetKlinesAsync(symbol, CandleInterval.D1, now.Date - day * FetchDays,
            FetchDays + 1, ct);
        // Bỏ ngày đang chạy: giá đóng của nó chưa chốt.
        var candles = klines
            .Where(k => k.OpenTimeUtc + day <= now)
            .Select(k => new Candle(client.Exchange, symbol, CandleInterval.D1, k.OpenTimeUtc, k.Open, k.High, k.Low,
                k.Close, k.Volume, isClosed: true, receivedAtUtc: now, quoteVolume: k.QuoteVolume,
                tradeCount: k.TradeCount, takerBuyVolume: k.TakerBuyVolume))
            .ToList();
        _cache[symbol] = (now, candles);
        return candles;
    }
}
