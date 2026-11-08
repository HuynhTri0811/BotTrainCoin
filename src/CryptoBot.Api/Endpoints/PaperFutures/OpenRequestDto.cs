using CryptoBot.Application.Futures;
using CryptoBot.Domain.Futures;

namespace CryptoBot.Api.Endpoints;

public sealed record OpenRequestDto(Guid CommandId, string Symbol, string Side, int Leverage, decimal Margin,
    decimal StopLoss, decimal? TakeProfit)
{
    public OpenPositionRequest? ToRequest() =>
        Enum.TryParse<PositionSide>(Side, ignoreCase: true, out var side) && Enum.IsDefined(side)
            ? new OpenPositionRequest(CommandId, (Symbol ?? "").ToUpperInvariant(), side, Leverage, Margin, StopLoss, TakeProfit)
            : null;
}
