namespace CryptoBot.Infrastructure.Exchanges.Binance;

public sealed class BinanceOptions
{
    /// <summary>REST base URL cho dữ liệu thị trường công khai (không cần API key).</summary>
    public string MarketDataBaseUrl { get; set; } = "https://api.binance.com";

    /// <summary>REST base URL USDⓈ-M Futures cho mark price công khai.</summary>
    public string FuturesMarketDataBaseUrl { get; set; } = "https://fapi.binance.com";
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
