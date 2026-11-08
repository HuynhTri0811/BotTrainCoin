namespace CryptoBot.Api.Endpoints;

public sealed record PositionDto(long Id, string Environment, string Symbol, string Side, int Leverage,
    decimal Quantity, decimal EntryPrice, decimal Margin, decimal LiquidationPrice, decimal StopLoss,
    decimal? TakeProfit, decimal OpenFee, decimal CloseFee, string Status, string? CloseReason, decimal? ExitPrice,
    decimal? RealizedPnl, decimal? NetPnl, DateTime OpenedAtUtc, DateTime? ClosedAtUtc, decimal? MarkPrice,
    decimal? UnrealizedPnl, decimal? RoePercent);
