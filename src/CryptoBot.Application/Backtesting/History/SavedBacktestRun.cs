using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Application.Backtesting;

/// <summary>Một lần backtest đã lưu kèm kết quả đầy đủ đã giải nén.</summary>
public sealed record SavedBacktestRun(SavedBacktest Saved, BacktestSnapshot Snapshot);
