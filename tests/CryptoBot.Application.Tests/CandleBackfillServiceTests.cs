using CryptoBot.Application.Abstractions;
using CryptoBot.Application.MarketData;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.Application.Tests;

public class CandleBackfillServiceTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly CandleKey Key = new("Binance", "BTCUSDT", CandleInterval.H1);

    private readonly FakeClock _clock = new(T0 + TimeSpan.FromHours(100));
    private readonly InMemoryCandleStore _store = new();
    private readonly FakeMarketDataClient _client;

    public CandleBackfillServiceTests() => _client = new FakeMarketDataClient(_clock);

    private CandleBackfillService CreateService(int batchSize = 1000) =>
        new(_client, _store, new MarketDataOptions { BatchSize = batchSize }, NullLogger<CandleBackfillService>.Instance);

    private static DateTime Hour(int index) => T0 + TimeSpan.FromHours(index);

    private void AddKlines(int from, int count, decimal price = 100)
    {
        for (var index = from; index < from + count; index++)
            _client.Klines.Add(new KlineDto(Hour(index), price, price, price, price, 1));
    }

    private void StoreCandles(int from, int count, decimal price)
    {
        for (var index = from; index < from + count; index++)
            _store.Candles[Hour(index)] = new Candle("Binance", "BTCUSDT", CandleInterval.H1, Hour(index), price,
                price, price, price, 1, true, T0);
    }

    [Fact]
    public async Task Downloads_only_candles_older_than_the_earliest_stored_one()
    {
        AddKlines(0, 100, price: 100);
        StoreCandles(60, 40, price: 200);

        var result = await CreateService(batchSize: 25).BackfillAsync(Key, Hour(10), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(50, result.Inserted);
        Assert.Equal(Hour(10), result.FirstOpenTimeUtc);
        Assert.False(result.ReachedRequestLimit);
        // Nến đã có không bị đụng tới.
        Assert.All(_store.Candles.Values.Where(candle => candle.OpenTimeUtc >= Hour(60)),
            candle => Assert.Equal(200m, candle.Close));
        Assert.All(_store.Candles.Values, candle => Assert.True(candle.IsClosed));
    }

    [Fact]
    public async Task Stops_paging_once_it_reaches_the_stored_candles()
    {
        AddKlines(0, 100);
        StoreCandles(30, 70, price: 100);

        var result = await CreateService(batchSize: 10).BackfillAsync(Key, Hour(0), CancellationToken.None);

        Assert.Equal(30, result!.Inserted);
        // 0–9, 10–19, 20–29: tới nến đã có thì dừng, không hỏi thêm trang nào.
        Assert.Equal(3, result.Requests);
    }

    [Fact]
    public async Task Empty_store_downloads_up_to_the_running_candle()
    {
        AddKlines(0, 101);

        var result = await CreateService().BackfillAsync(Key, Hour(0), CancellationToken.None);

        // Nến đang chạy (giờ 100) để đồng bộ thường lo.
        Assert.Equal(100, result!.Inserted);
        Assert.DoesNotContain(Hour(100), _store.Candles.Keys);
    }

    [Fact]
    public async Task Other_exchange_is_rejected()
    {
        var key = new CandleKey("Bitfinex", "BTCUSD", CandleInterval.D1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().BackfillAsync(key, Hour(0), CancellationToken.None));
    }
}
