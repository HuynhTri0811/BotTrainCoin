namespace CryptoBot.Domain.MarketData;

/// <summary>Một lần mở cửa của một sàn chứng khoán.</summary>
/// <param name="Market">Sàn mở cửa.</param>
/// <param name="OpenUtc">Lúc mở cửa, UTC.</param>
public sealed record MarketOpening(StockMarket Market, DateTime OpenUtc);
