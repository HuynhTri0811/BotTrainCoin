using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Futures;
using CryptoBot.Domain.Futures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CryptoBot.Infrastructure.Persistence;

public sealed class PaperFuturesStore(BotDbContext db) : IPaperFuturesStore
{
    private const string Env = "Paper";

    public async Task<IReadOnlyList<FuturesPosition>> GetOpenPositionsAsync(CancellationToken ct) =>
        await db.FuturesPositions
            .Where(position => position.Environment == Env && position.Status == PositionStatus.Open)
            .OrderBy(position => position.Id).ToListAsync(ct);

    /// <summary>
    /// Đọc vị thế với trạng thái mới nhất trong database. Truy vấn lại không làm mới entity đã tracking, nên vị thế
    /// đã được DbContext này đọc trước đó (ví dụ khi worker kiểm tra SL/TP) được tải lại để không quyết định đóng
    /// trên trạng thái cũ.
    /// </summary>
    public async Task<FuturesPosition?> GetAsync(long id, CancellationToken ct)
    {
        FuturesPosition? tracked = db.FuturesPositions.Local.FirstOrDefault(position => position.Id == id);
        if (tracked is null)
            return await db.FuturesPositions.FirstOrDefaultAsync(
                position => position.Environment == Env && position.Id == id, ct);

        EntityEntry<FuturesPosition> entry = db.Entry(tracked);
        if (entry.State == EntityState.Added)
            return tracked;
        await entry.ReloadAsync(ct);
        return entry.State == EntityState.Detached ? null : tracked;
    }

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

    /// <summary>
    /// Lưu vị thế và bút toán trong một transaction. Vị thế mang concurrency token (xmin của PostgreSQL): nếu bên
    /// khác đã đổi vị thế sau khi đọc (ví dụ đã đóng), cả lần lưu bị hủy, các thay đổi Paper chưa lưu bị bỏ và vị thế
    /// được đọc lại, rồi ném <see cref="ConcurrentUpdateException"/>. Nhờ vậy không thể ghi bút toán đóng hai lần.
    /// </summary>
    public async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await DiscardPendingAsync(ct);
            throw new ConcurrentUpdateException("Vị thế đã bị thay đổi bởi thao tác khác; không lưu thay đổi.",
                exception);
        }
    }

    private async Task DiscardPendingAsync(CancellationToken ct)
    {
        foreach (EntityEntry<PaperLedgerEntry> entry in db.ChangeTracker.Entries<PaperLedgerEntry>()
                     .Where(tracked => tracked.State == EntityState.Added).ToList())
            entry.State = EntityState.Detached;

        foreach (EntityEntry<FuturesPosition> entry in db.ChangeTracker.Entries<FuturesPosition>()
                     .Where(tracked => tracked.State is EntityState.Added or EntityState.Modified).ToList())
        {
            if (entry.State == EntityState.Added)
                entry.State = EntityState.Detached;
            else
                await entry.ReloadAsync(ct);
        }
    }
}
