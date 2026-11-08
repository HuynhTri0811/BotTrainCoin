using CryptoBot.Application.Signals;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Tests;

internal sealed class InMemorySignalStore : ISignalStore
{
    public readonly List<TradingSignal> Signals = [];
    public int Saves;

    public Task<TradingSignal?> GetLastAsync(int strategyInstanceId, int configVersion, CancellationToken ct) =>
        Task.FromResult(Signals
            .Where(signal => signal.StrategyInstanceId == strategyInstanceId && signal.ConfigVersion == configVersion)
            .MaxBy(signal => signal.CandleOpenTimeUtc));

    public Task SaveEvaluationAsync(StrategyInstance instance, IReadOnlyList<TradingSignal> newSignals, CancellationToken ct)
    {
        foreach (var signal in newSignals)
        {
            if (Signals.Any(candidate => candidate.StrategyInstanceId == signal.StrategyInstanceId
                && candidate.ConfigVersion == signal.ConfigVersion &&
                                 candidate.CandleOpenTimeUtc == signal.CandleOpenTimeUtc
                                     && candidate.Action == signal.Action))
                throw new InvalidOperationException("Unique constraint violated");
        }
        Signals.AddRange(newSignals);
        Saves++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TradingSignal>> QueryAsync(SignalFilter filter, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TradingSignal>>(Signals.ToList());
}
