using CryptoBot.Application.Futures;
using CryptoBot.Domain.Futures;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.Infrastructure.Persistence;

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
