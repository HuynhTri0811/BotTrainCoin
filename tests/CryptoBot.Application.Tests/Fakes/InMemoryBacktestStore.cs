using CryptoBot.Application.Backtesting;
using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Application.Tests;

internal sealed class InMemoryBacktestStore : IBacktestStore
{
    public readonly List<SavedBacktest> Saved = [];
    public int Saves;

    public Task<IReadOnlyList<BacktestSummary>> ListAsync(int? strategyInstanceId, int limit, int offset,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<BacktestSummary>>(ForInstance(strategyInstanceId)
            .OrderByDescending(saved => saved.CreatedAtUtc).ThenByDescending(saved => saved.Id)
            .Skip(offset)
            .Take(limit)
            .Select(ToSummary)
            .ToList());

    public Task<BacktestSummary?> GetSummaryAsync(long id, CancellationToken ct) =>
        Task.FromResult(Saved.Where(saved => saved.Id == id).Select(ToSummary).FirstOrDefault());

    public Task<int> CountAsync(int? strategyInstanceId, CancellationToken ct) =>
        Task.FromResult(ForInstance(strategyInstanceId).Count());

    private static BacktestSummary ToSummary(SavedBacktest saved) => new(saved.Id, saved.CreatedAtUtc,
        saved.StrategyInstanceId, saved.Symbol, saved.Interval, saved.FastPeriod, saved.SlowPeriod,
        saved.LookbackCandles, saved.FirstCandleUtc, saved.LastCandleUtc, saved.Settings, saved.TotalTrades,
        saved.WinRatePercent, saved.NetProfit, saved.NetProfitPercent, saved.MaxDrawdownPercent, saved.Note);

    private IEnumerable<SavedBacktest> ForInstance(int? strategyInstanceId) =>
        Saved.Where(saved => strategyInstanceId is null || saved.StrategyInstanceId == strategyInstanceId);

    public Task<SavedBacktest?> GetAsync(long id, CancellationToken ct) =>
        Task.FromResult(Saved.FirstOrDefault(saved => saved.Id == id));

    public void Add(SavedBacktest backtest)
    {
        typeof(SavedBacktest).GetProperty(nameof(SavedBacktest.Id))!.SetValue(backtest, (long)Saved.Count + 1);
        Saved.Add(backtest);
    }

    public Task<bool> SetNoteAsync(long id, string? note, CancellationToken ct)
    {
        var saved = Saved.FirstOrDefault(saved => saved.Id == id);
        saved?.SetNote(note);
        return Task.FromResult(saved is not null);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken ct) =>
        Task.FromResult(Saved.RemoveAll(saved => saved.Id == id) > 0);

    public Task SaveAsync(CancellationToken ct)
    {
        Saves++;
        return Task.CompletedTask;
    }
}
