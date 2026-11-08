using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Abstractions;

public interface ISignalStore
{
    Task<TradingSignal?> GetLastAsync(int strategyInstanceId, int configVersion, CancellationToken ct);

    /// <summary>
    /// Ghi các signal mới và checkpoint của instance trong cùng một transaction.
    /// </summary>
    Task SaveEvaluationAsync(StrategyInstance instance, IReadOnlyList<TradingSignal> newSignals, CancellationToken ct);

    Task<IReadOnlyList<TradingSignal>> QueryAsync(SignalFilter filter, int limit, CancellationToken ct);
}
