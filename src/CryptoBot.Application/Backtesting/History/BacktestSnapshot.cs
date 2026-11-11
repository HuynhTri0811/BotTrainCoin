using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Backtesting;

/// <summary>
/// Phần kết quả backtest được lưu để mở lại: mọi thứ trang Backtest hiển thị, trừ đường vốn từng nến (đã có trong
/// <see cref="Chart"/> ở dạng gộp).
/// </summary>
public sealed record BacktestSnapshot(
    EmaCrossParameters Parameters,
    BacktestSettings Settings,
    DateTime? FirstCandleUtc,
    DateTime? LastCandleUtc,
    int CandleCount,
    int WarmupCandles,
    int EvaluatedCandles,
    int SkippedGapWindows,
    int BuySignals,
    int SellSignals,
    BacktestStatistics Statistics,
    IReadOnlyList<BacktestTrade> Trades,
    BacktestOpenTrade? OpenTrade,
    BacktestChart Chart)
{
    public static BacktestSnapshot From(BacktestRun run) => new(
        run.Parameters,
        run.Result.Settings,
        run.FirstCandleUtc,
        run.LastCandleUtc,
        run.CandleCount,
        run.WarmupCandles,
        run.Result.EvaluatedCandles,
        run.Result.SkippedGapWindows,
        run.Result.Signals.Count(signal => signal.Action == SignalAction.Buy),
        run.Result.Signals.Count(signal => signal.Action == SignalAction.Sell),
        run.Result.Statistics,
        run.Result.Trades,
        run.Result.OpenTrade,
        run.Chart);
}
