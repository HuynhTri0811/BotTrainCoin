namespace CryptoBot.Api.Endpoints;

public sealed record LivePricesDto(int IntervalSeconds, DateTime ServerTimeUtc, IReadOnlyList<LiveSymbolDto> Symbols);
