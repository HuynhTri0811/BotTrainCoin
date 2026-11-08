using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;
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
