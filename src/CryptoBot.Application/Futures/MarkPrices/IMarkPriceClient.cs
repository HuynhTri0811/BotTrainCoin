namespace CryptoBot.Application.Futures;

public interface IMarkPriceClient
{
    Task<MarkPrice> GetMarkPriceAsync(string symbol, CancellationToken ct);
}
