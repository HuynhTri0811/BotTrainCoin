namespace CryptoBot.Application.Abstractions;

public interface IMarkPriceClient
{
    Task<MarkPrice> GetMarkPriceAsync(string symbol, CancellationToken ct);
}
