namespace CryptoBot.Application.Abstractions;

public sealed record MarkPrice(string Symbol, decimal Price, DateTime TimeUtc);
