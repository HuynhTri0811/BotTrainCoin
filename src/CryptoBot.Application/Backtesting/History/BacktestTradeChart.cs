namespace CryptoBot.Application.Backtesting;

/// <summary>
/// Nến quanh một lệnh của lần backtest đã lưu; mỗi cột gộp <paramref name="CandlesPerBar"/> nến (thường là 1).
/// </summary>
public sealed record BacktestTradeChart(int CandlesPerBar, IReadOnlyList<BacktestChartBar> Bars);
