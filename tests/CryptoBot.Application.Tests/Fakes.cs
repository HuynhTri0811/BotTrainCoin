using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Tests;

internal sealed class FakeClock(DateTime now) : IClock
{
    public DateTime UtcNow { get; set; } = now;
}

internal sealed class InMemoryCandleStore : ICandleStore
{
    public readonly SortedDictionary<DateTime, Candle> Candles = new();

    public Task<DateTime?> GetLatestOpenTimeAsync(CandleKey key, CancellationToken ct) =>
        Task.FromResult(Candles.Count == 0 ? (DateTime?)null : Candles.Keys.Max());

    public Task<UpsertResult> UpsertAsync(CandleKey key, IReadOnlyList<Candle> candles, CancellationToken ct)
    {
        int inserted = 0, updated = 0, unchanged = 0;
        var conflicts = new List<CandleIssue>();
        foreach (var c in candles)
        {
            if (!Candles.TryGetValue(c.OpenTimeUtc, out var stored))
            {
                Candles[c.OpenTimeUtc] = c;
                inserted++;
            }
            else if (stored.IsClosed)
            {
                if (stored.ApplyUpdate(c)) unchanged++;
                else conflicts.Add(new CandleIssue(CandleIssueKind.InvalidValues, c.OpenTimeUtc, "conflict"));
            }
            else
            {
                stored.ApplyUpdate(c);
                updated++;
            }
        }
        return Task.FromResult(new UpsertResult(inserted, updated, unchanged, conflicts));
    }

    public Task<IReadOnlyList<Candle>> GetLatestAsync(CandleKey key, DateTime? beforeExclusiveUtc, int limit,
        bool closedOnly, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Candle>>(Candles.Values
            .Where(c => (beforeExclusiveUtc is null || c.OpenTimeUtc < beforeExclusiveUtc) && (!closedOnly || c.IsClosed))
            .TakeLast(Math.Max(limit, 0)).ToList());

    public Task<IReadOnlyList<Candle>> GetAfterAsync(CandleKey key, DateTime? afterExclusiveUtc, int limit,
        bool closedOnly, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Candle>>(Candles.Values
            .Where(c => (afterExclusiveUtc is null || c.OpenTimeUtc > afterExclusiveUtc) && (!closedOnly || c.IsClosed))
            .Take(Math.Max(limit, 0)).ToList());
}

internal sealed class InMemorySignalStore : ISignalStore
{
    public readonly List<TradingSignal> Signals = [];
    public int Saves;

    public Task<TradingSignal?> GetLastAsync(int strategyInstanceId, int configVersion, CancellationToken ct) =>
        Task.FromResult(Signals
            .Where(s => s.StrategyInstanceId == strategyInstanceId && s.ConfigVersion == configVersion)
            .MaxBy(s => s.CandleOpenTimeUtc));

    public Task SaveEvaluationAsync(StrategyInstance instance, IReadOnlyList<TradingSignal> newSignals, CancellationToken ct)
    {
        foreach (var s in newSignals)
        {
            if (Signals.Any(x => x.StrategyInstanceId == s.StrategyInstanceId && x.ConfigVersion == s.ConfigVersion &&
                                 x.CandleOpenTimeUtc == s.CandleOpenTimeUtc && x.Action == s.Action))
                throw new InvalidOperationException("Unique constraint violated");
        }
        Signals.AddRange(newSignals);
        Saves++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TradingSignal>> QueryAsync(int? strategyInstanceId, int? configVersion,
        DateTime? fromUtc, DateTime? toUtc, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TradingSignal>>(Signals.ToList());
}

internal sealed class FakeMarketDataClient(FakeClock clock) : IMarketDataClient
{
    public readonly List<KlineDto> Klines = [];
    public int Calls;

    public string Exchange => "Binance";

    public Task<DateTime> GetServerTimeAsync(CancellationToken ct) => Task.FromResult(clock.UtcNow);

    public Task<IReadOnlyList<KlineDto>> GetKlinesAsync(string symbol, CandleInterval interval,
        DateTime startTimeUtc, int limit, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult<IReadOnlyList<KlineDto>>(Klines
            .Where(k => k.OpenTimeUtc >= startTimeUtc && k.OpenTimeUtc <= clock.UtcNow)
            .OrderBy(k => k.OpenTimeUtc).Take(limit).ToList());
    }
}
