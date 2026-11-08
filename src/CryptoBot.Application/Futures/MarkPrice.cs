namespace CryptoBot.Application.Futures;

public sealed record MarkPrice(string Symbol, decimal Price, DateTime TimeUtc);
