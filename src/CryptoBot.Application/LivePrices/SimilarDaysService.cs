using CryptoBot.Application.Abstractions;
using CryptoBot.Application.MarketData;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.LivePrices;

/// <param name="AsOfDayUtc">Ngày (nến đã đóng) cuối cùng dùng để dự báo.</param>
/// <param name="AnchorPrice">Giá đóng cửa của ngày đó trên sàn của bảng giá (Binance); khoảng giá tính từ đây.</param>
/// <param name="TargetCloseUtc">Thời điểm đóng cửa được dự báo (cuối ngày hôm nay với 24 giờ).</param>
/// <param name="Low68">Giá: AnchorPrice × (1 + Low68Percent / 100).</param>
public sealed record SimilarDaysResult(
    string HistorySymbol,
    string Exchange,
    DateTime AsOfDayUtc,
    decimal AnchorPrice,
    DateTime TargetCloseUtc,
    decimal Low68,
    decimal High68,
    SimilarDaysForecast Forecast,
    SimilarDaysBacktest Backtest);

/// <summary>
/// Dự báo và chạy thử chỉ đổi khi có nến ngày mới, nên mỗi (symbol, mốc) giữ đúng một kết quả theo ngày cuối:
/// có ngày mới thì thay, không phình theo thời gian. Lazy đảm bảo nhiều request cùng lúc chỉ tính một lần.
/// </summary>
public sealed class SimilarDaysCache
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Symbol, int Horizon), Entry> _entries = new();

    public sealed record Computed(SimilarDaysForecast? Forecast, SimilarDaysBacktest Backtest);

    private sealed record Entry(DateTime LastDay, Lazy<Task<Computed>> Value);

    public async Task<Computed> GetOrComputeAsync(string symbol, int horizon, DateTime lastDay,
        Func<Task<Computed>> compute)
    {
        Entry entry;
        lock (_gate)
        {
            if (!_entries.TryGetValue((symbol, horizon), out entry!) || entry.LastDay != lastDay)
                _entries[(symbol, horizon)] = entry = new Entry(lastDay, new Lazy<Task<Computed>>(compute));
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

/// <summary>
/// Dự báo "ngày tương tự" cho symbol của bảng giá trực tiếp (ví dụ BTCUSDT), dùng nến ngày lịch sử dài hạn
/// trong DB (DailyHistory, ví dụ BTCUSD trên Bitfinex từ 2016).
///
/// Dự báo đo thay đổi từ giá đóng ngày D đến giá đóng ngày D + horizon, nên khoảng giá được neo vào giá đóng
/// ngày D (không phải giá hiện tại, vốn đã gồm phần giá chạy trong ngày). Chỉ trả kết quả khi D là hôm qua
/// (UTC): lịch sử cũ hơn nghĩa là dự báo cho một ngày đã qua.
/// </summary>
public sealed class SimilarDaysService(
    ICandleStore candles,
    IDailyHistoryClient historyClient,
    DailyHistoryOptions options,
    DailyRangeService daily,
    SimilarDaysCache cache,
    IClock clock,
    ILogger<SimilarDaysService> logger)
{
    /// <summary>Đủ cho hơn 50 năm nến ngày.</summary>
    private const int MaxDailyCandles = 20_000;

    /// <summary>
    /// Null khi symbol không có lịch sử dài hạn, chưa đủ dữ liệu, lịch sử chưa cập nhật đến hôm qua, hoặc có lỗi
    /// (đã ghi log). Không bao giờ ném lỗi, để thanh biên độ vẫn hiện khi phần này hỏng.
    /// </summary>
    public async Task<SimilarDaysResult?> GetAsync(string liveSymbol, int horizonDays, CancellationToken ct)
    {
        try
        {
            return await ComputeAsync(liveSymbol, horizonDays, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ngày tương tự {Symbol}: bỏ qua do lỗi", liveSymbol);
            return null;
        }
    }

    private async Task<SimilarDaysResult?> ComputeAsync(string liveSymbol, int horizonDays, CancellationToken ct)
    {
        var historySymbol = ToHistorySymbol(liveSymbol);
        if (historySymbol is null)
            return null;

        var day = CandleInterval.D1.Duration;
        var yesterday = clock.UtcNow.Date - day;
        var key = new CandleKey(historyClient.Exchange, historySymbol, CandleInterval.D1);
        var lastStored = (await candles.GetLatestAsync(key, null, 1, closedOnly: true, ct)).FirstOrDefault();
        if (lastStored is null || lastStored.OpenTimeUtc != yesterday)
            return null;

        // Neo vào giá đóng hôm qua trên sàn của bảng giá; hai sàn cùng chốt ngày theo UTC.
        var anchor = (await daily.GetClosedDailyCandlesAsync(liveSymbol, ct)).LastOrDefault();
        if (anchor is null || anchor.OpenTimeUtc != yesterday)
            return null;

        var computed = await cache.GetOrComputeAsync(historySymbol, horizonDays, lastStored.OpenTimeUtc, async () =>
        {
            var history = await candles.GetLatestAsync(key, null, MaxDailyCandles, closedOnly: true, ct);
            var closes = history.Select(c => c.Close).ToList();
            // Chạy thử nặng (vài trăm ms đến hơn 1 giây): đưa khỏi luồng xử lý request.
            return await Task.Run(() => new SimilarDaysCache.Computed(
                SimilarDaysForecast.Compute(closes, horizonDays),
                SimilarDaysForecast.Backtest(closes, horizonDays)), ct);
        });
        if (computed.Forecast is not { } f)
            return null;

        var price = anchor.Close;
        return new SimilarDaysResult(historySymbol, historyClient.Exchange, yesterday, price,
            yesterday + day * (horizonDays + 1), price * (1 + f.Low68Percent / 100), price * (1 + f.High68Percent / 100),
            f, computed.Backtest);
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
