using CryptoBot.Application.Signals;
using CryptoBot.Domain.Strategies;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.Infrastructure.Persistence;

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
