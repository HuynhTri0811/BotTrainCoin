using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Application.Backtesting;

/// <summary>Một dòng trong lịch sử backtest: thông số và con số chính, không kèm kết quả đầy đủ.</summary>
/// <remarks>
/// <see cref="FastPeriod"/>/<see cref="SlowPeriod"/> null khi tín hiệu là chỉ báo tự viết; tham số chỉ báo nằm ở
/// <see cref="SignalParameters"/> dạng JSON (đọc bằng <see cref="BacktestSignalSpec.ParseParameters"/>).
/// <see cref="LookbackCandles"/> là cửa sổ nến của tín hiệu.
/// </remarks>
public sealed record BacktestSummary(
    long Id,
    DateTime CreatedAtUtc,
    int StrategyInstanceId,
    string Symbol,
    string Interval,
    string SignalType,
    string? SignalParameters,
    int? FastPeriod,
    int? SlowPeriod,
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
