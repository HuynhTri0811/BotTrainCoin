using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Tests;

internal sealed class FakeMarketDataClient(FakeClock clock) : IMarketDataClient
{
    public readonly List<KlineDto> Klines = [];
    public int Calls;

    /// <summary>Chờ trước khi trả nến, để test giữ một lần tải đang chạy.</summary>
    public Func<Task>? BeforeKlines;

    public string Exchange => "Binance";

    public Task<DateTime> GetServerTimeAsync(CancellationToken ct) => Task.FromResult(clock.UtcNow);

    public Task<decimal> GetLastPriceAsync(string symbol, CancellationToken ct) => Task.FromResult(100m);

    public async Task<IReadOnlyList<KlineDto>> GetKlinesAsync(string symbol, CandleInterval interval,
        DateTime startTimeUtc, int limit, CancellationToken ct)
    {
        Calls++;
        if (BeforeKlines is { } before)
            await before();
        return Klines
            .Where(kline => kline.OpenTimeUtc >= startTimeUtc && kline.OpenTimeUtc <= clock.UtcNow)
            .OrderBy(kline => kline.OpenTimeUtc).Take(limit).ToList();
    }
}
