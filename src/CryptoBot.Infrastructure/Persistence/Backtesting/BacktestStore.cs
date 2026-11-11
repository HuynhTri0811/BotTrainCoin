using CryptoBot.Application.Backtesting;
using CryptoBot.Domain.Backtesting;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.Infrastructure.Persistence;

public sealed class BacktestStore(BotDbContext db) : IBacktestStore
{
    public async Task<IReadOnlyList<BacktestSummary>> ListAsync(int? strategyInstanceId, int limit,
        CancellationToken ct) =>
        // Chỉ chọn các cột tóm tắt: không đọc cột nội dung nén (vài trăm KB mỗi dòng).
        await db.SavedBacktests.AsNoTracking()
            .Where(saved => strategyInstanceId == null || saved.StrategyInstanceId == strategyInstanceId)
            .OrderByDescending(saved => saved.CreatedAtUtc).ThenByDescending(saved => saved.Id)
            .Take(limit)
            .Select(saved => new BacktestSummary(saved.Id, saved.CreatedAtUtc, saved.StrategyInstanceId,
                saved.Symbol, saved.Interval, saved.FastPeriod, saved.SlowPeriod, saved.LookbackCandles,
                saved.FirstCandleUtc, saved.LastCandleUtc,
                new BacktestSettings(saved.InitialCapital, saved.Sizing, saved.SizeValue, saved.TakeProfitPercent,
                    saved.StopLossPercent, saved.FeePercent),
                saved.TotalTrades, saved.WinRatePercent, saved.NetProfit, saved.NetProfitPercent,
                saved.MaxDrawdownPercent, saved.Note))
            .ToListAsync(ct);

    public Task<SavedBacktest?> GetAsync(long id, CancellationToken ct) =>
        db.SavedBacktests.FirstOrDefaultAsync(saved => saved.Id == id, ct);

    public void Add(SavedBacktest backtest) => db.SavedBacktests.Add(backtest);

    public void Remove(SavedBacktest backtest) => db.SavedBacktests.Remove(backtest);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
