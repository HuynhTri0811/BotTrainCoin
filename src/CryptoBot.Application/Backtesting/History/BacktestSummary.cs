using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Application.Backtesting;

/// <summary>Một dòng trong lịch sử backtest: thông số và con số chính, không kèm kết quả đầy đủ.</summary>
public sealed record BacktestSummary(
    long Id,
    DateTime CreatedAtUtc,
    int StrategyInstanceId,
    string Symbol,
    string Interval,
    int FastPeriod,
    int SlowPeriod,
    int LookbackCandles,
    DateTime? FirstCandleUtc,
    DateTime? LastCandleUtc,
    BacktestSettings Settings,
    int TotalTrades,
    decimal WinRatePercent,
    decimal NetProfit,
    decimal NetProfitPercent,
    decimal MaxDrawdownPercent,
    string? Note);
