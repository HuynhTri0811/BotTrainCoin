namespace CryptoBot.Api.Endpoints;

public sealed record MarkPriceDto(string Symbol, decimal Price, DateTime TimeUtc);
