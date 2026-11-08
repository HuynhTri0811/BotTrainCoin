namespace CryptoBot.Application.MarketData;

public sealed record MonthEvent(int Year, int Month, string Title, string? Detail, bool MarketWide);
