using CryptoBot.Application.Backtesting;
using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Application.Tests;

internal sealed class InMemoryBacktestStore : IBacktestStore
{
    public readonly List<SavedBacktest> Saved = [];
    public int Saves;

    public Task<IReadOnlyList<BacktestSummary>> ListAsync(int? strategyInstanceId, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<BacktestSummary>>(Saved
            .Where(saved => strategyInstanceId is null || saved.StrategyInstanceId == strategyInstanceId)
            .OrderByDescending(saved => saved.CreatedAtUtc).ThenByDescending(saved => saved.Id)
            .Take(limit)
            .Select(saved => new BacktestSummary(saved.Id, saved.CreatedAtUtc, saved.StrategyInstanceId, saved.Symbol,
                saved.Interval, saved.FastPeriod, saved.SlowPeriod, saved.LookbackCandles, saved.FirstCandleUtc,
                saved.LastCandleUtc, saved.Settings, saved.TotalTrades, saved.WinRatePercent, saved.NetProfit,
                saved.NetProfitPercent, saved.MaxDrawdownPercent, saved.Note))
            .ToList());

    public Task<SavedBacktest?> GetAsync(long id, CancellationToken ct) =>
        Task.FromResult(Saved.FirstOrDefault(saved => saved.Id == id));

    public void Add(SavedBacktest backtest)
    {
        typeof(SavedBacktest).GetProperty(nameof(SavedBacktest.Id))!.SetValue(backtest, (long)Saved.Count + 1);
        Saved.Add(backtest);
    }

    public void Remove(SavedBacktest backtest) => Saved.Remove(backtest);

    public Task SaveAsync(CancellationToken ct)
    {
        Saves++;
        return Task.CompletedTask;
    }
}
