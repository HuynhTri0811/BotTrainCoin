using CryptoBot.Application.Signals;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Tests;

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
