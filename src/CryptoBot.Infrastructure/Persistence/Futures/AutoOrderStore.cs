using CryptoBot.Application.Futures;
using CryptoBot.Domain.Futures;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.Infrastructure.Persistence;

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
