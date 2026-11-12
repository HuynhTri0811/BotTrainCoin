using CryptoBot.Domain.Strategies;

namespace CryptoBot.Api.Endpoints;

/// <summary>Một dòng trong lịch sử backtest (không kèm lệnh và chart); Parameters null khi tín hiệu là chỉ báo.
/// </summary>
public sealed record BacktestSummaryDto(
    long Id,
    DateTime CreatedAtUtc,
    int StrategyId,
    string Symbol,
    string Interval,
    EmaCrossParameters? Parameters,
    BacktestSignalDto Signal,
    DateTime? FirstCandleUtc,
    DateTime? LastCandleUtc,
    BacktestSettingsDto Settings,
    int TotalTrades,
    decimal WinRatePercent,
    decimal NetProfit,
    decimal NetProfitPercent,
    decimal MaxDrawdownPercent,
    string? Note);
