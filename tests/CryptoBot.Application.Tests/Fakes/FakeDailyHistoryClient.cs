using CryptoBot.Application.Abstractions;

namespace CryptoBot.Application.Tests;

internal sealed class FakeDailyHistoryClient : IDailyHistoryClient
{
    public readonly List<KlineDto> Klines = [];
    public readonly List<DateTime> Requests = [];

    public string Exchange => "Bitfinex";

    public Task<IReadOnlyList<KlineDto>> GetDailyKlinesAsync(string symbol, DateTime startTimeUtc, int limit,
        CancellationToken ct)
    {
        Requests.Add(startTimeUtc);
        return Task.FromResult<IReadOnlyList<KlineDto>>(
            Klines.Where(kline => kline.OpenTimeUtc >= startTimeUtc).OrderBy(kline => kline.OpenTimeUtc).Take(limit)
                .ToList());
    }
}
