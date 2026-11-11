namespace CryptoBot.Api.Endpoints;

/// <summary>Một lệnh đã đóng. ExitReason: Signal, TakeProfit hoặc StopLoss.</summary>
public sealed record BacktestTradeDto(
    DateTime EntryTimeUtc,
    decimal EntryPrice,
    DateTime ExitTimeUtc,
    decimal ExitPrice,
    decimal Quantity,
    decimal Fees,
    decimal NetPnl,
    decimal ReturnPercent,
    string ExitReason);
