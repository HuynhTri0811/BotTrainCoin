using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Tests;

internal sealed class FakeMarketDataClient(FakeClock clock) : IMarketDataClient
{
    public readonly List<KlineDto> Klines = [];
    public int Calls;

    public string Exchange => "Binance";

    public Task<DateTime> GetServerTimeAsync(CancellationToken ct) => Task.FromResult(clock.UtcNow);

    public Task<decimal> GetLastPriceAsync(string symbol, CancellationToken ct) => Task.FromResult(100m);

    public Task<IReadOnlyList<KlineDto>> GetKlinesAsync(string symbol, CandleInterval interval,
        DateTime startTimeUtc, int limit, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult<IReadOnlyList<KlineDto>>(Klines
            .Where(kline => kline.OpenTimeUtc >= startTimeUtc && kline.OpenTimeUtc <= clock.UtcNow)
            .OrderBy(kline => kline.OpenTimeUtc).Take(limit).ToList());
    }
}
