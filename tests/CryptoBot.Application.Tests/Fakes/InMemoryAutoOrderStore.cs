using CryptoBot.Application.Futures;
using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Tests;

internal sealed class InMemoryAutoOrderStore : IAutoOrderStore
{
    public readonly List<AutoOrderRule> Rules = [];
    private long _nextId = 1;

    public Task<IReadOnlyList<AutoOrderRule>> GetActiveAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AutoOrderRule>>(Rules.Where(rule => rule.Status == AutoRuleStatus.Active)
            .ToList());

    public Task<IReadOnlyList<AutoOrderRule>> GetWatchingCloseAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AutoOrderRule>>(Rules.Where(rule => rule.IsWatchingClose).ToList());

    public Task<IReadOnlyList<AutoOrderRule>> GetRecentAsync(int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AutoOrderRule>>(Rules.AsEnumerable().Reverse().Take(limit).ToList());

    public Task<AutoOrderRule?> GetAsync(long id, CancellationToken ct) =>
        Task.FromResult(Rules.FirstOrDefault(rule => rule.Id == id));

    public void Add(AutoOrderRule rule)
    {
        typeof(AutoOrderRule).GetProperty(nameof(AutoOrderRule.Id))!.SetValue(rule, _nextId++);
        Rules.Add(rule);
    }

    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
}
