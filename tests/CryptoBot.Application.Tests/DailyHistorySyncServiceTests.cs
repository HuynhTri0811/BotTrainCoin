using CryptoBot.Application.Abstractions;
using CryptoBot.Application.MarketData;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.Application.Tests;

public class DailyHistorySyncServiceTests
{
    private static readonly DateTime D0 = new(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeClock _clock = new(D0);
    private readonly InMemoryCandleStore _store = new();
    private readonly FakeDailyHistoryClient _client = new();

    private DailyHistorySyncService CreateService(int batchSize = 10000) =>
        new(_client, _store, _clock, new DailyHistoryOptions { BatchSize = batchSize },
            NullLogger<DailyHistorySyncService>.Instance);

    private void AddDays(params int[] days)
    {
        foreach (var d in days)
            _client.Klines.Add(new KlineDto(D0.AddDays(d), 100 + d, 110 + d, 90 + d, 105 + d, 1));
    }

    [Fact]
    public async Task Bootstrap_starts_from_start_date_and_keeps_today_open()
    {
        AddDays(0, 1, 2, 3);
        _clock.UtcNow = D0.AddDays(3.5);

        var inserted = await CreateService().SyncAsync("BTCUSD", CancellationToken.None);

        Assert.Equal(4, inserted);
        Assert.Equal(D0, _client.Requests[0]);
        Assert.True(_store.Candles[D0.AddDays(2)].IsClosed);
        Assert.False(_store.Candles[D0.AddDays(3)].IsClosed);
        Assert.Equal(105m, _store.Candles[D0].Close);
    }

    [Fact]
    public async Task Resync_continues_from_latest_and_keeps_days_around_exchange_gaps()
    {
        AddDays(0, 1);
        _clock.UtcNow = D0.AddDays(1.5);
        await CreateService().SyncAsync("BTCUSD", CancellationToken.None);

        // Ngày 2 không có giao dịch trên sàn nên không có nến.
        AddDays(3, 4);
        _clock.UtcNow = D0.AddDays(4.5);
        var inserted = await CreateService().SyncAsync("BTCUSD", CancellationToken.None);

        Assert.Equal(2, inserted);
        Assert.Equal(D0.AddDays(1), _client.Requests[^1]);
        Assert.True(_store.Candles[D0.AddDays(1)].IsClosed);
        Assert.Equal(4, _store.Candles.Count);
    }

    [Fact]
    public async Task Pages_until_batch_is_short()
    {
        AddDays(Enumerable.Range(0, 25).ToArray());
        _clock.UtcNow = D0.AddDays(30);

        var inserted = await CreateService(batchSize: 10).SyncAsync("BTCUSD", CancellationToken.None);

        Assert.Equal(25, inserted);
        Assert.Equal(3, _client.Requests.Count);
    }
}

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
            Klines.Where(k => k.OpenTimeUtc >= startTimeUtc).OrderBy(k => k.OpenTimeUtc).Take(limit).ToList());
    }
}
