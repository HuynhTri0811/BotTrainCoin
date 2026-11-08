using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public sealed record OpenPositionRequest(
    Guid CommandId,
    string Symbol,
    PositionSide Side,
    int Leverage,
    decimal Margin,
    decimal StopLoss,
    decimal? TakeProfit);
