using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public sealed record PositionView(FuturesPosition Position, decimal? MarkPrice, decimal? UnrealizedPnl, decimal? RoePercent);
