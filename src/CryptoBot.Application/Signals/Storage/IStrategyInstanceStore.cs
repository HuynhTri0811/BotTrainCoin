using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Signals;

public interface IStrategyInstanceStore
{
    Task<IReadOnlyList<StrategyInstance>> GetAllAsync(CancellationToken ct);
    Task<StrategyInstance?> GetAsync(int id, CancellationToken ct);
    Task<StrategyInstance?> FindByNameAsync(string name, CancellationToken ct);
    Task AddAsync(StrategyInstance instance, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
