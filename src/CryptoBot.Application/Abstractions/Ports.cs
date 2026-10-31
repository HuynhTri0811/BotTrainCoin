using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed record CandleKey(string Exchange, string Symbol, CandleInterval Interval)
{
    public override string ToString() => $"{Exchange}:{Symbol}:{Interval.Code}";
}

/// <summary>Một kline thô từ sàn. Các cột thống kê để null khi sàn không trả (ví dụ Bitfinex).</summary>
public sealed record KlineDto(
    DateTime OpenTimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume,
    decimal? QuoteVolume = null,
    long? TradeCount = null,
    decimal? TakerBuyVolume = null)
{
    /// <summary>
    /// Nến của <paramref name="key"/> nhận lúc <paramref name="receivedAtUtc"/> (giờ sàn nếu có). Nến đã đóng khi
    /// thời điểm nhận ≥ OpenTime + một khung.
    /// </summary>
    public Candle ToCandle(CandleKey key, DateTime receivedAtUtc) =>
        new(key.Exchange, key.Symbol, key.Interval, OpenTimeUtc, Open, High, Low, Close, Volume,
            isClosed: receivedAtUtc >= OpenTimeUtc + key.Interval.Duration,
            receivedAtUtc: receivedAtUtc, quoteVolume: QuoteVolume, tradeCount: TradeCount,
            takerBuyVolume: TakerBuyVolume);
}

public interface IMarketDataClient
{
    string Exchange { get; }

    Task<DateTime> GetServerTimeAsync(CancellationToken ct);

    /// <summary>Giá giao dịch gần nhất (Spot) của symbol.</summary>
    Task<decimal> GetLastPriceAsync(string symbol, CancellationToken ct);

    /// <summary>Klines tăng dần theo OpenTime, bắt đầu từ <paramref name="startTimeUtc"/> (bao gồm).</summary>
    Task<IReadOnlyList<KlineDto>> GetKlinesAsync(string symbol, CandleInterval interval,
        DateTime startTimeUtc, int limit, CancellationToken ct);
}

/// <summary>Nến ngày lịch sử dài hạn (từ 2016) của một sàn ngoài Binance, dùng để lưu và truy vấn.</summary>
public interface IDailyHistoryClient
{
    string Exchange { get; }

    /// <summary>Nến 1d tăng dần theo OpenTime, bắt đầu từ <paramref name="startTimeUtc"/> (bao gồm).</summary>
    Task<IReadOnlyList<KlineDto>> GetDailyKlinesAsync(string symbol, DateTime startTimeUtc, int limit,
        CancellationToken ct);
}

public sealed record UpsertResult(int Inserted, int Updated, int Unchanged, IReadOnlyList<CandleIssue> Conflicts);

public interface ICandleStore
{
    Task<DateTime?> GetLatestOpenTimeAsync(CandleKey key, CancellationToken ct);

    /// <summary>OpenTime sớm nhất (từ <paramref name="fromUtc"/>) của nến chưa có số giao dịch hoặc khối lượng mua chủ động; null nếu đủ.</summary>
    Task<DateTime?> GetFirstMissingStatsOpenTimeAsync(CandleKey key, DateTime fromUtc, CancellationToken ct);

    Task<UpsertResult> UpsertAsync(CandleKey key, IReadOnlyList<Candle> candles, CancellationToken ct);

    /// <summary>Tối đa <paramref name="limit"/> nến mới nhất có OpenTime &lt; before (nếu có), trả về tăng dần.</summary>
    Task<IReadOnlyList<Candle>> GetLatestAsync(CandleKey key, DateTime? beforeExclusiveUtc, int limit,
        bool closedOnly, CancellationToken ct);

    /// <summary>Tối đa <paramref name="limit"/> nến đầu tiên có OpenTime &gt; after (nếu có), tăng dần.</summary>
    Task<IReadOnlyList<Candle>> GetAfterAsync(CandleKey key, DateTime? afterExclusiveUtc, int limit,
        bool closedOnly, CancellationToken ct);
}

public interface IStrategyInstanceStore
{
    Task<IReadOnlyList<StrategyInstance>> GetAllAsync(CancellationToken ct);
    Task<StrategyInstance?> GetAsync(int id, CancellationToken ct);
    Task<StrategyInstance?> FindByNameAsync(string name, CancellationToken ct);
    Task AddAsync(StrategyInstance instance, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

/// <summary>Điều kiện lọc tín hiệu; trường null là không lọc. Thời gian tính theo giờ mở nến, gồm cả hai đầu.</summary>
public sealed record SignalFilter(
    int? StrategyInstanceId = null,
    int? ConfigVersion = null,
    SignalAction? Action = null,
    bool? IsBackfill = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);

public interface ISignalStore
{
    Task<TradingSignal?> GetLastAsync(int strategyInstanceId, int configVersion, CancellationToken ct);

    /// <summary>
    /// Ghi các signal mới và checkpoint của instance trong cùng một transaction.
    /// </summary>
    Task SaveEvaluationAsync(StrategyInstance instance, IReadOnlyList<TradingSignal> newSignals, CancellationToken ct);

    Task<IReadOnlyList<TradingSignal>> QueryAsync(SignalFilter filter, int limit, CancellationToken ct);
}

/// <summary>Sàn tạm thời không phục vụ dữ liệu (rate limit, lỗi mạng, 5xx).</summary>
public sealed class MarketDataUnavailableException(string message, TimeSpan? retryAfter = null, Exception? inner = null)
    : Exception(message, inner)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
