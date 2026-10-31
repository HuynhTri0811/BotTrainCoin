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

internal sealed class InMemoryStrategyInstanceStore : IStrategyInstanceStore
{
    public readonly List<StrategyInstance> Instances = [];
    public int Saves;

    public Task<IReadOnlyList<StrategyInstance>> GetAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StrategyInstance>>(Instances.ToList());

    public Task<StrategyInstance?> GetAsync(int id, CancellationToken ct) =>
        Task.FromResult(Instances.FirstOrDefault(instance => instance.Id == id));

    public Task<StrategyInstance?> FindByNameAsync(string name, CancellationToken ct) =>
        Task.FromResult(Instances.FirstOrDefault(instance => instance.Name == name));

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
            .Where(signal => signal.StrategyInstanceId == strategyInstanceId && signal.ConfigVersion == configVersion)
            .MaxBy(signal => signal.CandleOpenTimeUtc));

    public Task SaveEvaluationAsync(StrategyInstance instance, IReadOnlyList<TradingSignal> newSignals, CancellationToken ct)
    {
        foreach (var signal in newSignals)
        {
            if (Signals.Any(candidate => candidate.StrategyInstanceId == signal.StrategyInstanceId
                && candidate.ConfigVersion == signal.ConfigVersion &&
                                 candidate.CandleOpenTimeUtc == signal.CandleOpenTimeUtc
                                     && candidate.Action == signal.Action))
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
            .Where(kline => kline.OpenTimeUtc >= startTimeUtc && kline.OpenTimeUtc <= clock.UtcNow)
            .OrderBy(kline => kline.OpenTimeUtc).Take(limit).ToList());
    }
}

internal sealed class InMemoryDailyReportStore : IDailyReportStore
{
    public readonly List<DailyReport> Reports = [];
    private readonly List<DailyReport> _pending = [];

    public Task<bool> ExistsAsync(string symbol, DateTime periodEndUtc, CancellationToken ct) =>
        Task.FromResult(Reports.Any(report => report.Symbol == symbol && report.PeriodEndUtc == periodEndUtc));

    public Task<IReadOnlyList<DailyReport>> GetRecentAsync(string? symbol, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DailyReport>>(Reports
            .Where(report => symbol is null || report.Symbol == symbol)
            .OrderByDescending(report => report.PeriodEndUtc).Take(limit).ToList());

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
            .Where(article => article.Source == source && externalIds.Contains(article.ExternalId))
            .Select(article => article.ExternalId).ToHashSet());

    public Task<IReadOnlyList<NewsArticle>> QueryAsync(NewsFilter filter, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NewsArticle>>(Articles
            .Where(article => filter.Coin is null || article.Coins.Split(',').Contains(filter.Coin))
            .Where(article => filter.Source is null || article.Source == filter.Source)
            .Where(article => filter.Search is null || article.Title.Contains(filter.Search)
                || article.Summary.Contains(filter.Search))
            .Where(article => (filter.FromUtc is null || article.PublishedAtUtc >= filter.FromUtc) &&
                        (filter.ToUtc is null || article.PublishedAtUtc < filter.ToUtc))
            .OrderByDescending(article => article.PublishedAtUtc).Take(limit).ToList());

    public void AddRange(IEnumerable<NewsArticle> articles) => Articles.AddRange(articles);

    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
}
