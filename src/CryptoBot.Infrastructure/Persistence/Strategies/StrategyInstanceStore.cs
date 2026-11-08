using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.Strategies;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.Infrastructure.Persistence;

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
