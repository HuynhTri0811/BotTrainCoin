namespace CryptoBot.Api.Endpoints;

public sealed record QuoteDto(bool Accepted, IReadOnlyList<string> Errors, string Symbol, string Side, int Leverage,
    decimal? MarkPrice, decimal? FillPrice, decimal? Quantity, decimal? Notional, decimal? Margin, decimal? OpenFee,
    decimal? LiquidationPrice, decimal? LossAtStop, decimal? ProfitAtTakeProfit, decimal AvailableBalance);
