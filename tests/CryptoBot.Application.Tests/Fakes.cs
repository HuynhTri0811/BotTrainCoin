using CryptoBot.Application.Abstractions;
using CryptoBot.Application.News;
using CryptoBot.Application.Reports;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.News;
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

    public Task<DateTime?> GetFirstMissingStatsOpenTimeAsync(CandleKey key, DateTime fromUtc, CancellationToken ct) =>
        Task.FromResult(Candles.Values.FirstOrDefault(c => c.OpenTimeUtc >= fromUtc && (c.TradeCount is null || c.TakerBuyVolume is null))?.OpenTimeUtc);

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

internal sealed class InMemoryStrategyInstanceStore : IStrategyInstanceStore
{
    public readonly List<StrategyInstance> Instances = [];
    public int Saves;

    public Task<IReadOnlyList<StrategyInstance>> GetAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StrategyInstance>>(Instances.ToList());

    public Task<StrategyInstance?> GetAsync(int id, CancellationToken ct) =>
        Task.FromResult(Instances.FirstOrDefault(i => i.Id == id));

    public Task<StrategyInstance?> FindByNameAsync(string name, CancellationToken ct) =>
        Task.FromResult(Instances.FirstOrDefault(i => i.Name == name));

    public Task AddAsync(StrategyInstance instance, CancellationToken ct)
    {
        Instances.Add(instance);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct)
    {
        Saves++;
        return Task.CompletedTask;
    }
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

    public Task<IReadOnlyList<TradingSignal>> QueryAsync(SignalFilter filter, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TradingSignal>>(Signals.ToList());
}

internal sealed class FakeMarketDataClient(FakeClock clock) : IMarketDataClient
{
    public readonly List<KlineDto> Klines = [];
    public int Calls;

    public string Exchange => "Binance";

    public Task<DateTime> GetServerTimeAsync(CancellationToken ct) => Task.FromResult(clock.UtcNow);

    public Task<decimal> GetLastPriceAsync(string symbol, CancellationToken ct) => Task.FromResult(100m);

    public Task<IReadOnlyList<KlineDto>> GetKlinesAsync(string symbol, CandleInterval interval,
        DateTime startTimeUtc, int limit, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult<IReadOnlyList<KlineDto>>(Klines
            .Where(k => k.OpenTimeUtc >= startTimeUtc && k.OpenTimeUtc <= clock.UtcNow)
            .OrderBy(k => k.OpenTimeUtc).Take(limit).ToList());
    }
}

internal sealed class InMemoryDailyReportStore : IDailyReportStore
{
    public readonly List<DailyReport> Reports = [];
    private readonly List<DailyReport> _pending = [];

    public Task<bool> ExistsAsync(string symbol, DateTime periodEndUtc, CancellationToken ct) =>
        Task.FromResult(Reports.Any(r => r.Symbol == symbol && r.PeriodEndUtc == periodEndUtc));

    public Task<IReadOnlyList<DailyReport>> GetRecentAsync(string? symbol, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DailyReport>>(Reports
            .Where(r => symbol is null || r.Symbol == symbol)
            .OrderByDescending(r => r.PeriodEndUtc).Take(limit).ToList());

    public void Add(DailyReport report) => _pending.Add(report);

    public Task SaveAsync(CancellationToken ct)
    {
        Reports.AddRange(_pending);
        _pending.Clear();
        return Task.CompletedTask;
    }
}

internal sealed class FakeNewsFeedClient : INewsFeedClient
{
    public readonly Dictionary<string, List<NewsItemDto>> Feeds = [];
    public readonly HashSet<string> Failing = [];

    public Task<IReadOnlyList<NewsItemDto>> GetItemsAsync(string url, CancellationToken ct) =>
        Failing.Contains(url)
            ? throw new NewsFeedUnavailableException("503 Service Unavailable")
            : Task.FromResult<IReadOnlyList<NewsItemDto>>(Feeds.GetValueOrDefault(url) ?? []);
}

internal sealed class InMemoryNewsStore : INewsStore
{
    public readonly List<NewsArticle> Articles = [];

    public Task<IReadOnlySet<string>> GetExistingIdsAsync(string source, IReadOnlyCollection<string> externalIds,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<string>>(Articles
            .Where(a => a.Source == source && externalIds.Contains(a.ExternalId))
            .Select(a => a.ExternalId).ToHashSet());

    public Task<IReadOnlyList<NewsArticle>> QueryAsync(NewsFilter filter, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NewsArticle>>(Articles
            .Where(a => filter.Coin is null || a.Coins.Split(',').Contains(filter.Coin))
            .Where(a => filter.Source is null || a.Source == filter.Source)
            .Where(a => filter.Search is null || a.Title.Contains(filter.Search) || a.Summary.Contains(filter.Search))
            .Where(a => (filter.FromUtc is null || a.PublishedAtUtc >= filter.FromUtc) &&
                        (filter.ToUtc is null || a.PublishedAtUtc < filter.ToUtc))
            .OrderByDescending(a => a.PublishedAtUtc).Take(limit).ToList());

    public void AddRange(IEnumerable<NewsArticle> articles) => Articles.AddRange(articles);

    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
}
