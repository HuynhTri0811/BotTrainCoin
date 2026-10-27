using System.Collections.Concurrent;
using CryptoBot.Application.Abstractions;
using CryptoBot.Application.MarketData;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.LivePrices;

public sealed record SimilarDaysResult(
    string HistorySymbol,
    string Exchange,
    DateTime AsOfDayUtc,
    SimilarDaysForecast Forecast,
    SimilarDaysBacktest Backtest);

/// <summary>Kết quả chạy thử theo (symbol, ngày cuối): chỉ đổi khi có nến ngày mới.</summary>
public sealed class SimilarDaysCache
{
    public ConcurrentDictionary<(string Symbol, DateTime LastDay, int Horizon), SimilarDaysBacktest> Backtests { get; } = new();
}

/// <summary>
/// Dự báo "ngày tương tự" cho symbol của bảng giá trực tiếp (ví dụ BTCUSDT), dùng nến ngày lịch sử dài hạn
/// trong DB (DailyHistory, ví dụ BTCUSD trên Bitfinex từ 2016).
/// </summary>
public sealed class SimilarDaysService(
    ICandleStore candles,
    IDailyHistoryClient historyClient,
    DailyHistoryOptions options,
    SimilarDaysCache cache)
{
    /// <summary>Đủ cho hơn 50 năm nến ngày.</summary>
    private const int MaxDailyCandles = 20_000;

    /// <summary>Null khi symbol không có lịch sử dài hạn hoặc chưa đủ dữ liệu.</summary>
    public async Task<SimilarDaysResult?> GetAsync(string liveSymbol, int horizonDays, CancellationToken ct)
    {
        var historySymbol = ToHistorySymbol(liveSymbol);
        if (historySymbol is null)
            return null;

        var key = new CandleKey(historyClient.Exchange, historySymbol, CandleInterval.D1);
        var daily = await candles.GetLatestAsync(key, null, MaxDailyCandles, closedOnly: true, ct);
        if (daily.Count == 0)
            return null;

        var closes = daily.Select(c => c.Close).ToList();
        var forecast = SimilarDaysForecast.Compute(closes, horizonDays);
        if (forecast is null)
            return null;

        var lastDay = daily[^1].OpenTimeUtc;
        var backtest = cache.Backtests.GetOrAdd((historySymbol, lastDay, horizonDays),
            _ => SimilarDaysForecast.Backtest(closes, horizonDays));
        return new SimilarDaysResult(historySymbol, historyClient.Exchange, lastDay, forecast, backtest);
    }

    /// <summary>BTCUSDT → BTCUSD nếu BTCUSD có trong DailyHistory:Symbols.</summary>
    public string? ToHistorySymbol(string liveSymbol)
    {
        var candidate = liveSymbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)
            ? liveSymbol[..^1]
            : liveSymbol;
        return options.Symbols.FirstOrDefault(s => s.Equals(candidate, StringComparison.OrdinalIgnoreCase));
    }
}
