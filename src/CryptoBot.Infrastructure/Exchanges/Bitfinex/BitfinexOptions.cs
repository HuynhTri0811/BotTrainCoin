namespace CryptoBot.Infrastructure.Exchanges.Bitfinex;

public sealed class BitfinexOptions
{
    /// <summary>REST base URL công khai (không cần API key).</summary>
    public string BaseUrl { get; set; } = "https://api-pub.bitfinex.com";
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);
}
