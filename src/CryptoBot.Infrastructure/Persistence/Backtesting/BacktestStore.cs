using CryptoBot.Application.Backtesting;
using CryptoBot.Domain.Backtesting;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.Infrastructure.Persistence;

public sealed class BacktestStore(BotDbContext db) : IBacktestStore
{
    public async Task<IReadOnlyList<BacktestSummary>> ListAsync(int? strategyInstanceId, int limit, int offset,
        CancellationToken ct) =>
        await ForInstance(strategyInstanceId)
            .OrderByDescending(saved => saved.CreatedAtUtc).ThenByDescending(saved => saved.Id)
            .Skip(offset)
            .Take(limit)
            .Select(ToSummary)
            .ToListAsync(ct);

    public Task<BacktestSummary?> GetSummaryAsync(long id, CancellationToken ct) =>
        db.SavedBacktests.AsNoTracking().Where(saved => saved.Id == id).Select(ToSummary).FirstOrDefaultAsync(ct);

    public Task<int> CountAsync(int? strategyInstanceId, CancellationToken ct) =>
        ForInstance(strategyInstanceId).CountAsync(ct);

    public Task<SavedBacktest?> GetAsync(long id, CancellationToken ct) =>
        db.SavedBacktests.AsNoTracking().FirstOrDefaultAsync(saved => saved.Id == id, ct);

    // Sửa/xóa thẳng trong SQL: không đọc cột nội dung nén chỉ để đổi một ghi chú.
    public async Task<bool> SetNoteAsync(long id, string? note, CancellationToken ct) =>
        await db.SavedBacktests.Where(saved => saved.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(saved => saved.Note, note), ct) > 0;

    public async Task<bool> DeleteAsync(long id, CancellationToken ct) =>
        await db.SavedBacktests.Where(saved => saved.Id == id).ExecuteDeleteAsync(ct) > 0;

    public void Add(SavedBacktest backtest) => db.SavedBacktests.Add(backtest);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    // Chỉ chọn các cột tóm tắt: không đọc cột nội dung nén (vài trăm KB mỗi dòng).
    // Thiết lập Futures được dựng lại ở client sau khi đọc các cột.
    private static readonly Expression<Func<SavedBacktest, BacktestSummary>> ToSummary = saved => new BacktestSummary(
        saved.Id, saved.CreatedAtUtc, saved.StrategyInstanceId, saved.Symbol, saved.Interval, saved.SignalType,
        saved.SignalParameters, saved.FastPeriod, saved.SlowPeriod,
        saved.LookbackCandles, saved.FirstCandleUtc, saved.LastCandleUtc,
        new BacktestSettings(saved.InitialCapital, saved.Sizing, saved.SizeValue, saved.TakeProfitPercent,
            saved.StopLossPercent, saved.FeePercent, SavedBacktest.FuturesSettingsOf(saved.Leverage, saved.Direction,
                saved.CloseOnReverse, saved.SlippagePercent, saved.MaintenanceMarginPercent)),
        saved.TotalTrades, saved.WinRatePercent, saved.NetProfit, saved.NetProfitPercent, saved.MaxDrawdownPercent,
        saved.Note);

    private IQueryable<SavedBacktest> ForInstance(int? strategyInstanceId) => db.SavedBacktests.AsNoTracking()
        .Where(saved => strategyInstanceId == null || saved.StrategyInstanceId == strategyInstanceId);
}
