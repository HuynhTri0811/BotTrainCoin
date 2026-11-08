using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public sealed record OpenQuote(
    bool Accepted,
    IReadOnlyList<string> Errors,
    string Symbol,
    PositionSide Side,
    int Leverage,
    decimal? MarkPrice,
    decimal? FillPrice,
    decimal? Quantity,
    decimal? Notional,
    decimal? Margin,
    decimal? OpenFee,
    decimal? LiquidationPrice,
    decimal? LossAtStop,
    decimal? ProfitAtTakeProfit,
    decimal AvailableBalance);
