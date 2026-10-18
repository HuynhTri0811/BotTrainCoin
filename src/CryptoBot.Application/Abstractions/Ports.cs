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

/// <summary>Một kline thô từ sàn. CloseTimeUtc là thời điểm kết thúc nến do sàn báo.</summary>
public sealed record KlineDto(
    DateTime OpenTimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume);

public interface IMarketDataClient
{
    string Exchange { get; }

    Task<DateTime> GetServerTimeAsync(CancellationToken ct);

    /// <summary>Klines tăng dần theo OpenTime, bắt đầu từ <paramref name="startTimeUtc"/> (bao gồm).</summary>
    Task<IReadOnlyList<KlineDto>> GetKlinesAsync(string symbol, CandleInterval interval,
        DateTime startTimeUtc, int limit, CancellationToken ct);
}

public sealed record UpsertResult(int Inserted, int Updated, int Unchanged, IReadOnlyList<CandleIssue> Conflicts);

public interface ICandleStore
{
    Task<DateTime?> GetLatestOpenTimeAsync(CandleKey key, CancellationToken ct);

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

public interface ISignalStore
{
    Task<TradingSignal?> GetLastAsync(int strategyInstanceId, int configVersion, CancellationToken ct);

    /// <summary>
    /// Ghi các signal mới và checkpoint của instance trong cùng một transaction.
    /// </summary>
    Task SaveEvaluationAsync(StrategyInstance instance, IReadOnlyList<TradingSignal> newSignals, CancellationToken ct);

    Task<IReadOnlyList<TradingSignal>> QueryAsync(int? strategyInstanceId, int? configVersion,
        DateTime? fromUtc, DateTime? toUtc, int limit, CancellationToken ct);
}

/// <summary>Sàn tạm thời không phục vụ dữ liệu (rate limit, lỗi mạng, 5xx).</summary>
public sealed class MarketDataUnavailableException(string message, TimeSpan? retryAfter = null, Exception? inner = null)
    : Exception(message, inner)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
