using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Futures;
using CryptoBot.Application.News;
using CryptoBot.Application.Reports;
using CryptoBot.Domain.Futures;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.News;
using CryptoBot.Domain.Strategies;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.Infrastructure.Persistence;

public sealed class CandleStore(BotDbContext db) : ICandleStore
{
    public async Task<DateTime?> GetLatestOpenTimeAsync(CandleKey key, CancellationToken ct) =>
        await ForKey(key)
            .OrderByDescending(c => c.OpenTimeUtc)
            .Select(c => (DateTime?)c.OpenTimeUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<DateTime?> GetFirstMissingStatsOpenTimeAsync(CandleKey key, DateTime fromUtc,
        CancellationToken ct) =>
        await ForKey(key)
            .Where(c => c.OpenTimeUtc >= fromUtc && (c.TradeCount == null || c.TakerBuyVolume == null))
            .OrderBy(c => c.OpenTimeUtc)
            .Select(c => (DateTime?)c.OpenTimeUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<UpsertResult> UpsertAsync(CandleKey key, IReadOnlyList<Candle> candles, CancellationToken ct)
    {
        if (candles.Count == 0)
            return new UpsertResult(0, 0, 0, []);

        var from = candles.Min(c => c.OpenTimeUtc);
        var to = candles.Max(c => c.OpenTimeUtc);
        var existing = await ForKey(key, tracking: true)
            .Where(c => c.OpenTimeUtc >= from && c.OpenTimeUtc <= to)
            .ToDictionaryAsync(c => c.OpenTimeUtc, ct);

        int inserted = 0, updated = 0, unchanged = 0;
        var conflicts = new List<CandleIssue>();

        foreach (var candle in candles)
        {
            if (!existing.TryGetValue(candle.OpenTimeUtc, out var stored))
            {
                db.Candles.Add(candle);
                existing[candle.OpenTimeUtc] = candle;
                inserted++;
                continue;
            }

            var wasClosed = stored.IsClosed;
            if (!stored.ApplyUpdate(candle))
            {
                conflicts.Add(new CandleIssue(CandleIssueKind.InvalidValues, candle.OpenTimeUtc,
                    "Sàn trả giá trị khác cho nến đã đóng; giữ bản đã lưu."));
                continue;
            }

            if (wasClosed)
                unchanged++;
            else
                updated++;
        }

        await db.SaveChangesAsync(ct);
        return new UpsertResult(inserted, updated, unchanged, conflicts);
    }

    public async Task<IReadOnlyList<Candle>> GetLatestAsync(CandleKey key, DateTime? beforeExclusiveUtc, int limit,
        bool closedOnly, CancellationToken ct)
    {
        if (limit <= 0)
            return [];

        var q = ForKey(key);
        if (beforeExclusiveUtc is { } before)
            q = q.Where(c => c.OpenTimeUtc < before);
        if (closedOnly)
            q = q.Where(c => c.IsClosed);

        var list = await q.OrderByDescending(c => c.OpenTimeUtc).Take(limit).ToListAsync(ct);
        list.Reverse();
        return list;
    }

    public async Task<IReadOnlyList<Candle>> GetAfterAsync(CandleKey key, DateTime? afterExclusiveUtc, int limit,
        bool closedOnly, CancellationToken ct)
    {
        if (limit <= 0)
            return [];

        var q = ForKey(key);
        if (afterExclusiveUtc is { } after)
            q = q.Where(c => c.OpenTimeUtc > after);
        if (closedOnly)
            q = q.Where(c => c.IsClosed);

        return await q.OrderBy(c => c.OpenTimeUtc).Take(limit).ToListAsync(ct);
    }

    private IQueryable<Candle> ForKey(CandleKey key, bool tracking = false)
    {
        var interval = key.Interval.Code;
        var q = db.Candles.Where(c => c.Exchange == key.Exchange && c.Symbol == key.Symbol && c.Interval == interval);
        return tracking ? q : q.AsNoTracking();
    }
}

public sealed class StrategyInstanceStore(BotDbContext db) : IStrategyInstanceStore
{
    public async Task<IReadOnlyList<StrategyInstance>> GetAllAsync(CancellationToken ct) =>
        await db.StrategyInstances.OrderBy(s => s.Id).ToListAsync(ct);

    public Task<StrategyInstance?> GetAsync(int id, CancellationToken ct) =>
        db.StrategyInstances.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<StrategyInstance?> FindByNameAsync(string name, CancellationToken ct) =>
        db.StrategyInstances.FirstOrDefaultAsync(s => s.Name == name, ct);

    public async Task AddAsync(StrategyInstance instance, CancellationToken ct) =>
        await db.StrategyInstances.AddAsync(instance, ct);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class SignalStore(BotDbContext db) : ISignalStore
{
    public Task<TradingSignal?> GetLastAsync(int strategyInstanceId, int configVersion, CancellationToken ct) =>
        db.Signals.AsNoTracking()
            .Where(s => s.StrategyInstanceId == strategyInstanceId && s.ConfigVersion == configVersion)
            .OrderByDescending(s => s.CandleOpenTimeUtc)
            .FirstOrDefaultAsync(ct);

    public async Task SaveEvaluationAsync(StrategyInstance instance, IReadOnlyList<TradingSignal> newSignals,
        CancellationToken ct)
    {
        if (db.Entry(instance).State == EntityState.Detached)
            db.StrategyInstances.Update(instance);

        db.Signals.AddRange(newSignals);
        // Một SaveChanges = một transaction: signal và checkpoint cùng thành công hoặc cùng rollback.
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TradingSignal>> QueryAsync(int? strategyInstanceId, int? configVersion,
        DateTime? fromUtc, DateTime? toUtc, int limit, CancellationToken ct)
    {
        var q = db.Signals.AsNoTracking();
        if (strategyInstanceId is { } id)
            q = q.Where(s => s.StrategyInstanceId == id);
        if (configVersion is { } v)
            q = q.Where(s => s.ConfigVersion == v);
        if (fromUtc is { } from)
            q = q.Where(s => s.CandleOpenTimeUtc >= from);
        if (toUtc is { } to)
            q = q.Where(s => s.CandleOpenTimeUtc <= to);

        var list = await q.OrderByDescending(s => s.CandleOpenTimeUtc).ThenByDescending(s => s.Id)
            .Take(limit).ToListAsync(ct);
        list.Reverse();
        return list;
    }
}

public sealed class PaperFuturesStore(BotDbContext db) : IPaperFuturesStore
{
    private const string Env = "Paper";

    public async Task<IReadOnlyList<FuturesPosition>> GetOpenPositionsAsync(CancellationToken ct) =>
        await db.FuturesPositions
            .Where(p => p.Environment == Env && p.Status == PositionStatus.Open)
            .OrderBy(p => p.Id).ToListAsync(ct);

    public Task<FuturesPosition?> GetAsync(long id, CancellationToken ct) =>
        db.FuturesPositions.FirstOrDefaultAsync(p => p.Environment == Env && p.Id == id, ct);

    public Task<FuturesPosition?> FindByOpenCommandAsync(Guid commandId, CancellationToken ct) =>
        db.FuturesPositions.FirstOrDefaultAsync(p => p.Environment == Env && p.OpenCommandId == commandId, ct);

    public async Task<IReadOnlyList<FuturesPosition>> GetClosedAsync(int limit, CancellationToken ct) =>
        await db.FuturesPositions.AsNoTracking()
            .Where(p => p.Environment == Env && p.Status != PositionStatus.Open)
            .OrderByDescending(p => p.Id).Take(limit).ToListAsync(ct);

    public async Task<IReadOnlyList<PaperLedgerEntry>> GetLedgerAsync(CancellationToken ct)
    {
        // Bút toán chưa lưu (cùng transaction) cũng phải được tính vào số dư.
        var stored = await db.PaperLedger.Where(e => e.Environment == Env).ToListAsync(ct);
        var pending = db.ChangeTracker.Entries<PaperLedgerEntry>()
            .Where(e => e.State == EntityState.Added).Select(e => e.Entity);
        return stored.Concat(pending).Distinct().ToList();
    }

    public void AddPosition(FuturesPosition position) => db.FuturesPositions.Add(position);

    public void AddLedger(PaperLedgerEntry entry) => db.PaperLedger.Add(entry);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class AutoOrderStore(BotDbContext db) : IAutoOrderStore
{
    private const string Env = "Paper";

    public async Task<IReadOnlyList<AutoOrderRule>> GetActiveAsync(CancellationToken ct) =>
        await db.AutoOrderRules
            .Where(r => r.Environment == Env && r.Status == AutoRuleStatus.Active)
            .OrderBy(r => r.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<AutoOrderRule>> GetWatchingCloseAsync(CancellationToken ct) =>
        await db.AutoOrderRules
            .Where(r => r.Environment == Env && r.CloseOnReverse && r.Status == AutoRuleStatus.Triggered &&
                        r.CloseWatchEndedAtUtc == null)
            .OrderBy(r => r.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<AutoOrderRule>> GetRecentAsync(int limit, CancellationToken ct) =>
        await db.AutoOrderRules.AsNoTracking()
            .Where(r => r.Environment == Env)
            .OrderByDescending(r => r.Id).Take(limit).ToListAsync(ct);

    public Task<AutoOrderRule?> GetAsync(long id, CancellationToken ct) =>
        db.AutoOrderRules.FirstOrDefaultAsync(r => r.Environment == Env && r.Id == id, ct);

    public void Add(AutoOrderRule rule) => db.AutoOrderRules.Add(rule);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class DailyReportStore(BotDbContext db) : IDailyReportStore
{
    public Task<bool> ExistsAsync(string symbol, DateTime periodEndUtc, CancellationToken ct) =>
        db.DailyReports.AnyAsync(r => r.Symbol == symbol && r.PeriodEndUtc == periodEndUtc, ct);

    public async Task<IReadOnlyList<DailyReport>> GetRecentAsync(string? symbol, int limit, CancellationToken ct) =>
        await db.DailyReports.AsNoTracking()
            .Where(r => symbol == null || r.Symbol == symbol)
            .OrderByDescending(r => r.PeriodEndUtc).ThenBy(r => r.Symbol)
            .Take(limit).ToListAsync(ct);

    public void Add(DailyReport report) => db.DailyReports.Add(report);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class NewsStore(BotDbContext db) : INewsStore
{
    public async Task<IReadOnlySet<string>> GetExistingIdsAsync(string source, IReadOnlyCollection<string> externalIds,
        CancellationToken ct) =>
        (await db.NewsArticles
            .Where(a => a.Source == source && externalIds.Contains(a.ExternalId))
            .Select(a => a.ExternalId).ToListAsync(ct)).ToHashSet();

    public async Task<IReadOnlyList<NewsArticle>> QueryAsync(NewsFilter filter, int limit, CancellationToken ct)
    {
        var query = db.NewsArticles.AsNoTracking();
        // Coins là "BTC,ETH": mã coin không trùng một phần với nhau nên tìm chuỗi con là đủ.
        if (filter.Coin is { } coin)
            query = query.Where(a => a.Coins.Contains(coin));
        if (filter.Source is { } source)
            query = query.Where(a => a.Source == source);
        if (filter.Search is { } search)
        {
            // Giữ nguyên %, _ người dùng gõ. LIKE của SQLite chỉ không phân biệt hoa thường với ký tự ASCII.
            var pattern = $"%{search.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
            query = query.Where(a => EF.Functions.Like(a.Title, pattern, @"\") ||
                                     EF.Functions.Like(a.Summary, pattern, @"\"));
        }
        if (filter.FromUtc is { } from)
            query = query.Where(a => a.PublishedAtUtc >= from);
        if (filter.ToUtc is { } to)
            query = query.Where(a => a.PublishedAtUtc < to);
        return await query.OrderByDescending(a => a.PublishedAtUtc).ThenByDescending(a => a.Id)
            .Take(limit).ToListAsync(ct);
    }

    public void AddRange(IEnumerable<NewsArticle> articles) => db.NewsArticles.AddRange(articles);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
