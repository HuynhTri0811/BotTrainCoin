using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Tests;

internal sealed class InMemoryCandleStore : ICandleStore
{
    public readonly SortedDictionary<DateTime, Candle> Candles = new();

    public Task<DateTime?> GetLatestOpenTimeAsync(CandleKey key, CancellationToken ct) =>
        Task.FromResult(Candles.Count == 0 ? (DateTime?)null : Candles.Keys.Max());

    public Task<DateTime?> GetFirstMissingStatsOpenTimeAsync(CandleKey key, DateTime fromUtc, CancellationToken ct) =>
        Task.FromResult(Candles.Values.FirstOrDefault(candle => candle.OpenTimeUtc >= fromUtc
            && (candle.TradeCount is null || candle.TakerBuyVolume is null))?.OpenTimeUtc);

    public Task<UpsertResult> UpsertAsync(CandleKey key, IReadOnlyList<Candle> candles, CancellationToken ct)
    {
        int inserted = 0, updated = 0, unchanged = 0;
        var conflicts = new List<CandleIssue>();
        foreach (var candle in candles)
        {
            if (!Candles.TryGetValue(candle.OpenTimeUtc, out var stored))
            {
                Candles[candle.OpenTimeUtc] = candle;
                inserted++;
            }
            else if (stored.IsClosed)
            {
                if (stored.ApplyUpdate(candle)) unchanged++;
                else conflicts.Add(new CandleIssue(CandleIssueKind.InvalidValues, candle.OpenTimeUtc, "conflict"));
            }
            else
            {
                stored.ApplyUpdate(candle);
                updated++;
            }
        }
        return Task.FromResult(new UpsertResult(inserted, updated, unchanged, conflicts));
    }

    public Task<IReadOnlyList<Candle>> GetLatestAsync(CandleKey key, DateTime? beforeExclusiveUtc, int limit,
        bool closedOnly, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Candle>>(Candles.Values
            .Where(candle => (beforeExclusiveUtc is null || candle.OpenTimeUtc < beforeExclusiveUtc) && (!closedOnly
                || candle.IsClosed))
            .TakeLast(Math.Max(limit, 0)).ToList());

    public Task<IReadOnlyList<Candle>> GetAfterAsync(CandleKey key, DateTime? afterExclusiveUtc, int limit,
        bool closedOnly, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Candle>>(Candles.Values
            .Where(candle => (afterExclusiveUtc is null || candle.OpenTimeUtc > afterExclusiveUtc) && (!closedOnly
                || candle.IsClosed))
            .Take(Math.Max(limit, 0)).ToList());
}
