using CryptoBot.Application.Abstractions;

namespace CryptoBot.Application.Futures;

public sealed record AccountSnapshot(
    decimal WalletBalance,
    decimal UsedMargin,
    decimal UnrealizedPnl,
    decimal Equity,
    decimal AvailableBalance,
    IReadOnlyList<PositionView> OpenPositions,
    IReadOnlyList<MarkPrice> MarkPrices);
