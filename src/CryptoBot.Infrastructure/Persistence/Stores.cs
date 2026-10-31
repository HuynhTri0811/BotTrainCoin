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

/// <summary>
/// Lưu và đọc nến trong SQLite. Mỗi nến được xác định bởi (Exchange, Symbol, Interval, OpenTimeUtc), có index duy nhất.
/// </summary>
public sealed class CandleStore(BotDbContext db) : ICandleStore
{
    /// <summary>OpenTime của nến mới nhất đã lưu (kể cả nến đang chạy); null nếu chưa có nến nào.</summary>
    public async Task<DateTime?> GetLatestOpenTimeAsync(CandleKey key, CancellationToken ct) =>
        await ForKey(key)
            .OrderByDescending(candle => candle.OpenTimeUtc)
            .Select(candle => (DateTime?)candle.OpenTimeUtc)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// OpenTime sớm nhất từ <paramref name="fromUtc"/> của nến thiếu số giao dịch hoặc khối lượng mua chủ động.
    /// </summary>
    public async Task<DateTime?> GetFirstMissingStatsOpenTimeAsync(CandleKey key, DateTime fromUtc,
        CancellationToken ct) =>
        await ForKey(key)
            .Where(candle => candle.OpenTimeUtc >= fromUtc &&
                             (candle.TradeCount == null || candle.TakerBuyVolume == null))
            .OrderBy(candle => candle.OpenTimeUtc)
            .Select(candle => (DateTime?)candle.OpenTimeUtc)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Thêm nến mới và cập nhật nến đã có trong một lần lưu. Nến đã đóng không bị ghi đè: bản khác giá trị được báo
    /// trong <see cref="UpsertResult.Conflicts"/>, bản khớp chỉ được bổ sung các số còn thiếu và tính là Unchanged.
    /// </summary>
    public async Task<UpsertResult> UpsertAsync(CandleKey key, IReadOnlyList<Candle> candles, CancellationToken ct)
    {
        if (candles.Count == 0)
            return new UpsertResult(0, 0, 0, []);

        // Đọc một lần cả khoảng thời gian của lô, có tracking để SaveChanges ghi lại các nến được sửa.
        var from = candles.Min(candle => candle.OpenTimeUtc);
        var to = candles.Max(candle => candle.OpenTimeUtc);
        var existing = await ForKey(key, tracking: true)
            .Where(candle => candle.OpenTimeUtc >= from && candle.OpenTimeUtc <= to)
            .ToDictionaryAsync(candle => candle.OpenTimeUtc, ct);

        int inserted = 0, updated = 0, unchanged = 0;
        var conflicts = new List<CandleIssue>();

        foreach (var candle in candles)
        {
            if (!existing.TryGetValue(candle.OpenTimeUtc, out var stored))
            {
                db.Candles.Add(candle);
                // Ghi vào từ điển để nến trùng OpenTime trong cùng lô thành cập nhật, không thêm hai lần.
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

    /// <summary>
    /// Tối đa <paramref name="limit"/> nến mới nhất có OpenTime trước <paramref name="beforeExclusiveUtc"/> (nếu có),
    /// trả về cũ → mới. <paramref name="closedOnly"/> bỏ nến đang chạy.
    /// </summary>
    public async Task<IReadOnlyList<Candle>> GetLatestAsync(CandleKey key, DateTime? beforeExclusiveUtc, int limit,
        bool closedOnly, CancellationToken ct)
    {
        if (limit <= 0)
            return [];

        var query = ForKey(key);
        if (beforeExclusiveUtc is { } before)
            query = query.Where(candle => candle.OpenTimeUtc < before);
        if (closedOnly)
            query = query.Where(candle => candle.IsClosed);

        // Sắp giảm dần để Take lấy nến mới nhất, rồi đảo lại cho đúng thứ tự thời gian.
        var list = await query.OrderByDescending(candle => candle.OpenTimeUtc).Take(limit).ToListAsync(ct);
        list.Reverse();
        return list;
    }

    /// <summary>
    /// Tối đa <paramref name="limit"/> nến đầu tiên có OpenTime sau <paramref name="afterExclusiveUtc"/> (nếu có),
    /// cũ → mới. <paramref name="closedOnly"/> bỏ nến đang chạy.
    /// </summary>
    public async Task<IReadOnlyList<Candle>> GetAfterAsync(CandleKey key, DateTime? afterExclusiveUtc, int limit,
        bool closedOnly, CancellationToken ct)
    {
        if (limit <= 0)
            return [];

        var query = ForKey(key);
        if (afterExclusiveUtc is { } after)
            query = query.Where(candle => candle.OpenTimeUtc > after);
        if (closedOnly)
            query = query.Where(candle => candle.IsClosed);

        return await query.OrderBy(candle => candle.OpenTimeUtc).Take(limit).ToListAsync(ct);
    }

    /// <summary>Nến của một sàn/cặp/khung. Mặc định không tracking; chỉ bật khi cần sửa nến rồi lưu lại.</summary>
    private IQueryable<Candle> ForKey(CandleKey key, bool tracking = false)
    {
        var interval = key.Interval.Code;
        var query = db.Candles.Where(candle => candle.Exchange == key.Exchange && candle.Symbol == key.Symbol &&
                                               candle.Interval == interval);
        return tracking ? query : query.AsNoTracking();
    }
}

public sealed class StrategyInstanceStore(BotDbContext db) : IStrategyInstanceStore
{
    public async Task<IReadOnlyList<StrategyInstance>> GetAllAsync(CancellationToken ct) =>
        await db.StrategyInstances.OrderBy(instance => instance.Id).ToListAsync(ct);

    public Task<StrategyInstance?> GetAsync(int id, CancellationToken ct) =>
        db.StrategyInstances.FirstOrDefaultAsync(instance => instance.Id == id, ct);

    public Task<StrategyInstance?> FindByNameAsync(string name, CancellationToken ct) =>
        db.StrategyInstances.FirstOrDefaultAsync(instance => instance.Name == name, ct);

    public async Task AddAsync(StrategyInstance instance, CancellationToken ct) =>
        await db.StrategyInstances.AddAsync(instance, ct);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class SignalStore(BotDbContext db) : ISignalStore
{
    public Task<TradingSignal?> GetLastAsync(int strategyInstanceId, int configVersion, CancellationToken ct) =>
        db.Signals.AsNoTracking()
            .Where(signal => signal.StrategyInstanceId == strategyInstanceId && signal.ConfigVersion == configVersion)
            .OrderByDescending(signal => signal.CandleOpenTimeUtc)
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

    public async Task<IReadOnlyList<TradingSignal>> QueryAsync(SignalFilter filter, int limit, CancellationToken ct)
    {
        var query = db.Signals.AsNoTracking();
        if (filter.StrategyInstanceId is { } id)
            query = query.Where(signal => signal.StrategyInstanceId == id);
        if (filter.ConfigVersion is { } version)
            query = query.Where(signal => signal.ConfigVersion == version);
        if (filter.Action is { } action)
            query = query.Where(signal => signal.Action == action);
        if (filter.IsBackfill is { } backfill)
            query = query.Where(signal => signal.IsBackfill == backfill);
        if (filter.FromUtc is { } from)
            query = query.Where(signal => signal.CandleOpenTimeUtc >= from);
        if (filter.ToUtc is { } to)
            query = query.Where(signal => signal.CandleOpenTimeUtc <= to);

        var list = await query.OrderByDescending(signal => signal.CandleOpenTimeUtc)
            .ThenByDescending(signal => signal.Id).Take(limit).ToListAsync(ct);
        list.Reverse();
        return list;
    }
}

public sealed class PaperFuturesStore(BotDbContext db) : IPaperFuturesStore
{
    private const string Env = "Paper";

    public async Task<IReadOnlyList<FuturesPosition>> GetOpenPositionsAsync(CancellationToken ct) =>
        await db.FuturesPositions
            .Where(position => position.Environment == Env && position.Status == PositionStatus.Open)
            .OrderBy(position => position.Id).ToListAsync(ct);

    public Task<FuturesPosition?> GetAsync(long id, CancellationToken ct) =>
        db.FuturesPositions.FirstOrDefaultAsync(position => position.Environment == Env && position.Id == id, ct);

    public Task<FuturesPosition?> FindByOpenCommandAsync(Guid commandId, CancellationToken ct) =>
        db.FuturesPositions.FirstOrDefaultAsync(
            position => position.Environment == Env && position.OpenCommandId == commandId, ct);

    public async Task<IReadOnlyList<FuturesPosition>> GetClosedAsync(ClosedPositionFilter filter, int limit,
        CancellationToken ct)
    {
        var query = db.FuturesPositions.AsNoTracking()
            .Where(position => position.Environment == Env && position.Status != PositionStatus.Open);
        if (filter.Symbol is { } symbol)
            query = query.Where(position => position.Symbol == symbol);
        if (filter.Side is { } side)
            query = query.Where(position => position.Side == side);
        if (filter.CloseReason is { } reason)
            query = query.Where(position => position.CloseReason == reason);
        if (filter.FromUtc is { } from)
            query = query.Where(position => position.ClosedAtUtc >= from);
        if (filter.ToUtc is { } to)
            query = query.Where(position => position.ClosedAtUtc < to);
        return await query.OrderByDescending(position => position.Id).Take(limit).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PaperLedgerEntry>> GetLedgerAsync(CancellationToken ct)
    {
        // Bút toán chưa lưu (cùng transaction) cũng phải được tính vào số dư.
        var stored = await db.PaperLedger.Where(entry => entry.Environment == Env).ToListAsync(ct);
        var pending = db.ChangeTracker.Entries<PaperLedgerEntry>()
            .Where(tracked => tracked.State == EntityState.Added).Select(tracked => tracked.Entity);
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
            .Where(rule => rule.Environment == Env && rule.Status == AutoRuleStatus.Active)
            .OrderBy(rule => rule.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<AutoOrderRule>> GetWatchingCloseAsync(CancellationToken ct) =>
        await db.AutoOrderRules
            .Where(rule => rule.Environment == Env && rule.CloseOnReverse && rule.Status == AutoRuleStatus.Triggered &&
                        rule.CloseWatchEndedAtUtc == null)
            .OrderBy(rule => rule.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<AutoOrderRule>> GetRecentAsync(int limit, CancellationToken ct) =>
        await db.AutoOrderRules.AsNoTracking()
            .Where(rule => rule.Environment == Env)
            .OrderByDescending(rule => rule.Id).Take(limit).ToListAsync(ct);

    public Task<AutoOrderRule?> GetAsync(long id, CancellationToken ct) =>
        db.AutoOrderRules.FirstOrDefaultAsync(rule => rule.Environment == Env && rule.Id == id, ct);

    public void Add(AutoOrderRule rule) => db.AutoOrderRules.Add(rule);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class DailyReportStore(BotDbContext db) : IDailyReportStore
{
    public Task<bool> ExistsAsync(string symbol, DateTime periodEndUtc, CancellationToken ct) =>
        db.DailyReports.AnyAsync(report => report.Symbol == symbol && report.PeriodEndUtc == periodEndUtc, ct);

    public async Task<IReadOnlyList<DailyReport>> GetRecentAsync(string? symbol, int limit, CancellationToken ct) =>
        await db.DailyReports.AsNoTracking()
            .Where(report => symbol == null || report.Symbol == symbol)
            .OrderByDescending(report => report.PeriodEndUtc).ThenBy(report => report.Symbol)
            .Take(limit).ToListAsync(ct);

    public void Add(DailyReport report) => db.DailyReports.Add(report);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class NewsStore(BotDbContext db) : INewsStore
{
    public async Task<IReadOnlySet<string>> GetExistingIdsAsync(string source, IReadOnlyCollection<string> externalIds,
        CancellationToken ct) =>
        (await db.NewsArticles
            .Where(article => article.Source == source && externalIds.Contains(article.ExternalId))
            .Select(article => article.ExternalId).ToListAsync(ct)).ToHashSet();

    public async Task<IReadOnlyList<NewsArticle>> QueryAsync(NewsFilter filter, int limit, CancellationToken ct)
    {
        var query = db.NewsArticles.AsNoTracking();
        // Coins là "BTC,ETH": mã coin không trùng một phần với nhau nên tìm chuỗi con là đủ.
        if (filter.Coin is { } coin)
            query = query.Where(article => article.Coins.Contains(coin));
        if (filter.Source is { } source)
            query = query.Where(article => article.Source == source);
        if (filter.Search is { } search)
        {
            // Giữ nguyên %, _ người dùng gõ. LIKE của SQLite chỉ không phân biệt hoa thường với ký tự ASCII.
            var pattern = $"%{search.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
            query = query.Where(article => EF.Functions.Like(article.Title, pattern, @"\") ||
                                     EF.Functions.Like(article.Summary, pattern, @"\"));
        }
        if (filter.FromUtc is { } from)
            query = query.Where(article => article.PublishedAtUtc >= from);
        if (filter.ToUtc is { } to)
            query = query.Where(article => article.PublishedAtUtc < to);
        return await query.OrderByDescending(article => article.PublishedAtUtc).ThenByDescending(article => article.Id)
            .Take(limit).ToListAsync(ct);
    }

    public void AddRange(IEnumerable<NewsArticle> articles) => db.NewsArticles.AddRange(articles);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
