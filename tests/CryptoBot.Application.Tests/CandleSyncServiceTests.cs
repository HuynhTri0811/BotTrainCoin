using CryptoBot.Application.Abstractions;
using CryptoBot.Application.MarketData;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.Application.Tests;

public class CandleSyncServiceTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly CandleKey Key = new("Binance", "BTCUSDT", CandleInterval.H1);

    private readonly FakeClock _clock = new(T0);
    private readonly InMemoryCandleStore _store = new();
    private readonly FakeMarketDataClient _client;

    public CandleSyncServiceTests() => _client = new FakeMarketDataClient(_clock);

    private CandleSyncService CreateService(int batchSize = 1000, int bootstrap = 10) =>
        new(_client, _store, new MarketDataOptions { BatchSize = batchSize, BootstrapCandles = bootstrap },
            NullLogger<CandleSyncService>.Instance);

    private void AddKlines(int from, int count, decimal price = 100)
    {
        for (var i = from; i < from + count; i++)
            _client.Klines.Add(new KlineDto(T0 + TimeSpan.FromHours(i), price, price, price, price, 1));
    }

    [Fact]
    public async Task Bootstrap_marks_running_candle_as_not_closed()
    {
        AddKlines(0, 10);
        _clock.UtcNow = T0 + TimeSpan.FromHours(9.5);

        var result = await CreateService().SyncAsync(Key, CancellationToken.None);

        Assert.Equal(10, result.Inserted);
        Assert.False(_store.Candles[T0 + TimeSpan.FromHours(9)].IsClosed);
        Assert.True(_store.Candles[T0 + TimeSpan.FromHours(8)].IsClosed);
        Assert.Equal(T0 + TimeSpan.FromHours(8), result.LatestClosedOpenTimeUtc);
    }

    [Fact]
    public async Task Resync_after_downtime_backfills_gap_across_batches()
    {
        AddKlines(0, 10);
        _clock.UtcNow = T0 + TimeSpan.FromHours(9.5);
        await CreateService().SyncAsync(Key, CancellationToken.None);

        // "Mất kết nối" 25 giờ; batch nhỏ để buộc phân trang.
        AddKlines(10, 25);
        _clock.UtcNow = T0 + TimeSpan.FromHours(34.5);
        var result = await CreateService(batchSize: 10).SyncAsync(Key, CancellationToken.None);

        Assert.Equal(25, result.Inserted);
        Assert.Equal(35, _store.Candles.Count);
        Assert.True(_store.Candles[T0 + TimeSpan.FromHours(9)].IsClosed);
        Assert.Empty(CandleSeriesValidator.Validate(_store.Candles.Values.ToList(), CandleInterval.H1));
    }

    [Fact]
    public async Task Resync_with_only_running_candle_still_reports_latest_closed()
    {
        AddKlines(0, 10);
        _clock.UtcNow = T0 + TimeSpan.FromHours(9.5);
        await CreateService().SyncAsync(Key, CancellationToken.None);

        _clock.UtcNow = T0 + TimeSpan.FromHours(9.7);
        var result = await CreateService().SyncAsync(Key, CancellationToken.None);

        Assert.Equal(T0 + TimeSpan.FromHours(8), result.LatestClosedOpenTimeUtc);
    }

    [Fact]
    public async Task Invalid_candle_is_not_stored_and_is_reported()
    {
        AddKlines(0, 3);
        _client.Klines[1] = _client.Klines[1] with { High = 50 }; // High < Open
        _clock.UtcNow = T0 + TimeSpan.FromHours(3);

        var result = await CreateService(bootstrap: 3).SyncAsync(Key, CancellationToken.None);

        Assert.False(_store.Candles.ContainsKey(T0 + TimeSpan.FromHours(1)));
        Assert.Contains(result.Issues, i => i.Kind == CandleIssueKind.InvalidValues);
    }

    [Fact]
    public async Task Exchange_gap_is_reported_not_filled()
    {
        AddKlines(0, 3);
        AddKlines(5, 3);
        _clock.UtcNow = T0 + TimeSpan.FromHours(8);

        var result = await CreateService().SyncAsync(Key, CancellationToken.None);

        Assert.Contains(result.Issues, i => i.Kind == CandleIssueKind.Gap);
        Assert.Equal(6, _store.Candles.Count);
    }

    [Fact]
    public async Task Stores_trade_count_and_quote_volume()
    {
        _client.Klines.Add(new KlineDto(T0, 100, 100, 100, 100, 2, QuoteVolume: 200, TradeCount: 37));
        _clock.UtcNow = T0 + TimeSpan.FromHours(1);

        await CreateService().SyncAsync(Key, CancellationToken.None);

        var c = _store.Candles[T0];
        Assert.Equal(37, c.TradeCount);
        Assert.Equal(200m, c.QuoteVolume);
    }

    [Fact]
    public async Task Resync_backfills_stats_of_closed_candles_stored_without_them()
    {
        AddKlines(0, 5);                            // bản cũ: chưa có số giao dịch
        _clock.UtcNow = T0 + TimeSpan.FromHours(5);
        await CreateService().SyncAsync(Key, CancellationToken.None);
        Assert.All(_store.Candles.Values, c => Assert.Null(c.TradeCount));

        // Sàn trả lại đúng các nến đó, nay kèm số giao dịch.
        for (var i = 0; i < _client.Klines.Count; i++)
            _client.Klines[i] = _client.Klines[i] with { QuoteVolume = 100, TradeCount = 10 + i };
        var result = await CreateService().SyncAsync(Key, CancellationToken.None);

        Assert.Equal(0, result.Inserted);
        Assert.Empty(result.Issues);
        Assert.Equal([10L, 11L, 12L, 13L, 14L], _store.Candles.Values.Select(c => c.TradeCount!.Value));
    }
}
