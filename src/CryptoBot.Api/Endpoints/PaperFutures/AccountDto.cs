namespace CryptoBot.Api.Endpoints;

public sealed record AccountDto(string Environment, decimal WalletBalance, decimal UsedMargin, decimal UnrealizedPnl,
    decimal Equity, decimal AvailableBalance, IReadOnlyList<PositionDto> OpenPositions,
    IReadOnlyList<MarkPriceDto> MarkPrices, RulesDto Rules);
