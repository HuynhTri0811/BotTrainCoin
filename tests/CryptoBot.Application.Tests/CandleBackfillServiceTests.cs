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
    public async Task Empty_store_is_rejected_so_it_never_races_the_regular_sync()
    {
        AddKlines(0, 101);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().BackfillAsync(Key, Hour(0), CancellationToken.None));
        Assert.Empty(_store.Candles);
    }

    [Fact]
    public async Task Backfills_for_different_keys_do_not_block_each_other()
    {
        AddKlines(0, 10);
        StoreCandles(5, 5, price: 100);
        // Khóa là tĩnh: service và store riêng cho ETHUSDT vẫn dùng chung khóa với service BTCUSDT.
        var otherStore = new InMemoryCandleStore();
        otherStore.Candles[Hour(9)] =
            new Candle("Binance", "ETHUSDT", CandleInterval.H1, Hour(9), 1, 1, 1, 1, 1, true, T0);
        var otherService = new CandleBackfillService(new FakeMarketDataClient(_clock), otherStore,
            new MarketDataOptions(), NullLogger<CandleBackfillService>.Instance);
        // Giữ lần tải BTCUSDT đứng ở request đầu tiên để các lần sau gọi khi nó còn đang chạy.
        var gate = new TaskCompletionSource();
        _client.BeforeKlines = () => gate.Task;
        var service = CreateService();

        var running = service.BackfillAsync(Key, Hour(0), CancellationToken.None);
        var sameKey = await service.BackfillAsync(Key, Hour(0), CancellationToken.None);
        var otherKey = await otherService.BackfillAsync(Key with { Symbol = "ETHUSDT" }, Hour(0),
            CancellationToken.None);
        gate.SetResult();

        Assert.Null(sameKey);
        Assert.NotNull(otherKey);
        Assert.Equal(5, (await running)!.Inserted);
    }

    [Fact]
    public async Task Other_exchange_is_rejected()
    {
        var key = new CandleKey("Bitfinex", "BTCUSD", CandleInterval.D1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().BackfillAsync(key, Hour(0), CancellationToken.None));
    }

    [Fact]
    public async Task Finishing_on_the_last_allowed_request_is_not_reported_as_the_limit()
    {
        // Lô 2 nến: 999 request đầu lấy nến 0–1997, request thứ 1000 chỉ còn nến 1998 nên đã xong.
        _clock.UtcNow = Hour(4 * CandleBackfillService.MaxRequests);
        AddKlines(0, 2 * CandleBackfillService.MaxRequests - 1);
        StoreCandles(2 * CandleBackfillService.MaxRequests, 1, price: 100);

        var result = await CreateService(batchSize: 2).BackfillAsync(Key, Hour(0), CancellationToken.None);

        Assert.Equal(CandleBackfillService.MaxRequests, result!.Requests);
        Assert.False(result.ReachedRequestLimit);
    }

    [Fact]
    public async Task Running_out_of_requests_before_the_stored_candles_is_reported()
    {
        _clock.UtcNow = Hour(4 * CandleBackfillService.MaxRequests);
        AddKlines(0, 3 * CandleBackfillService.MaxRequests);
        StoreCandles(3 * CandleBackfillService.MaxRequests, 1, price: 100);

        var result = await CreateService(batchSize: 2).BackfillAsync(Key, Hour(0), CancellationToken.None);

        Assert.Equal(CandleBackfillService.MaxRequests, result!.Requests);
        Assert.True(result.ReachedRequestLimit);
    }
}
