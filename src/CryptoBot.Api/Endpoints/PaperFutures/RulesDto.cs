namespace CryptoBot.Api.Endpoints;

public sealed record RulesDto(int MaxLeverage, decimal TakerFeeRate, decimal SlippageRate, IReadOnlyList<string> Symbols);
