using CryptoBot.Application.Abstractions;
using CryptoBot.Application.LivePrices;
using CryptoBot.Application.MarketData;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.Application.Tests;

public class SimilarDaysServiceTests
{
    private static readonly DateTime D0 = new(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const int Days = 1000;
    private static readonly DateTime Yesterday = D0.AddDays(Days - 1);

    // Giữa trưa hôm nay: nến ngày cuối đã đóng là hôm qua.
    private readonly FakeClock _clock = new(D0.AddDays(Days).AddHours(12));
    private readonly RecordingCandleStore _store = new();
    private readonly SimilarDaysCache _cache = new();
    private readonly FakeMarketDataClient _binance;

    public SimilarDaysServiceTests()
    {
        _binance = new FakeMarketDataClient(_clock);
        AddAlternatingHistory(Days);
        AddBinanceClose(Yesterday, 200m);
    }

    private SimilarDaysService CreateService() =>
        new(_store, new FakeDailyHistoryClient(), new DailyHistoryOptions { Symbols = ["BTCUSD", "ETHUSD"] },
            new DailyRangeService(_binance, new LivePriceHistory(new LivePriceOptions()), _clock), _cache, _clock,
            NullLogger<SimilarDaysService>.Instance);

    /// <summary>Ngày lẻ tăng 1%, ngày chẵn giảm 1% (ngày cuối, chỉ số 999, là ngày tăng).</summary>
    private void AddAlternatingHistory(int count)
    {
        var close = 100m;
        for (var i = 0; i < count; i++)
        {
            var open = close;
            close = i == 0 ? 100m : close * (i % 2 == 1 ? 1.01m : 0.99m);
            var t = D0.AddDays(i);
            _store.Inner.Candles[t] = new Candle("Bitfinex", "BTCUSD", CandleInterval.D1, t, open,
                Math.Max(open, close), Math.Min(open, close), close, 1, isClosed: true, receivedAtUtc: t);
        }
    }

    private void AddBinanceClose(DateTime day, decimal close) =>
        _binance.Klines.Add(new KlineDto(day, close, close, close, close, 1));

    [Theory]
    [InlineData("BTCUSDT", "BTCUSD")]
    [InlineData("ethusdt", "ETHUSD")]
    [InlineData("BTCUSD", "BTCUSD")]
    [InlineData("SOLUSDT", null)]
    public void Maps_live_symbol_to_history_symbol(string live, string? expected) =>
        Assert.Equal(expected, CreateService().ToHistorySymbol(live));

    [Fact]
    public async Task Anchors_range_to_yesterdays_close_for_todays_close()
    {
        var r = await CreateService().GetAsync("BTCUSDT", 1, CancellationToken.None);

        Assert.NotNull(r);
        Assert.Equal((Yesterday, 200m, D0.AddDays(Days + 1)), (r.AsOfDayUtc, r.AnchorPrice, r.TargetCloseUtc));
        Assert.Equal(200m * (1 + r.Forecast.Low68Percent / 100), r.Low68);
        Assert.Equal(200m * (1 + r.Forecast.High68Percent / 100), r.High68);
        Assert.Equal(ForecastLean.Down, r.Forecast.Lean);
        // Đọc đúng lịch sử Bitfinex BTCUSD 1d, không phải symbol/sàn của bảng giá.
        Assert.All(_store.Keys, k => Assert.Equal(new CandleKey("Bitfinex", "BTCUSD", CandleInterval.D1), k));
    }

    [Fact]
    public async Task Stale_history_returns_null()
    {
        _clock.UtcNow = _clock.UtcNow.AddDays(2);
        AddBinanceClose(D0.AddDays(Days + 1), 210m);

        Assert.Null(await CreateService().GetAsync("BTCUSDT", 1, CancellationToken.None));
    }

    [Fact]
    public async Task Missing_anchor_close_returns_null()
    {
        _binance.Klines.Clear();

        Assert.Null(await CreateService().GetAsync("BTCUSDT", 1, CancellationToken.None));
    }

    [Fact]
    public async Task Errors_return_null_instead_of_throwing()
    {
        _store.Throw = true;

        Assert.Null(await CreateService().GetAsync("BTCUSDT", 1, CancellationToken.None));
        Assert.Equal(0, _cache.Count);
    }

    [Fact]
    public async Task Computes_once_per_day_and_replaces_the_old_day()
    {
        var service = CreateService();
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.GetAsync("BTCUSDT", 1, CancellationToken.None)));
        Assert.Equal(1, _store.FullHistoryReads);

        // Sang ngày mới: thay mục cũ, không giữ thêm.
        var today = D0.AddDays(Days);
        _store.Inner.Candles[today] = new Candle("Bitfinex", "BTCUSD", CandleInterval.D1, today, 100, 100, 100, 100, 1,
            isClosed: true, receivedAtUtc: today);
        AddBinanceClose(today, 205m);
        _clock.UtcNow = _clock.UtcNow.AddDays(1);
        var r = await CreateService().GetAsync("BTCUSDT", 1, CancellationToken.None);

        Assert.Equal(today, r!.AsOfDayUtc);
        Assert.Equal(2, _store.FullHistoryReads);
        Assert.Equal(1, _cache.Count);
    }

    /// <summary>Ghi lại khóa nến được hỏi và đếm số lần đọc cả lịch sử; có thể giả lập lỗi DB.</summary>
    private sealed class RecordingCandleStore : ICandleStore
    {
        public readonly InMemoryCandleStore Inner = new();
        public readonly List<CandleKey> Keys = [];
        public int FullHistoryReads;
        public bool Throw;

        public Task<IReadOnlyList<Candle>> GetLatestAsync(CandleKey key, DateTime? beforeExclusiveUtc, int limit,
            bool closedOnly, CancellationToken ct)
        {
            if (Throw)
                throw new InvalidOperationException("database is locked");
            lock (Keys)
            {
                Keys.Add(key);
                if (limit > 1)
                    FullHistoryReads++;
            }

            return Inner.GetLatestAsync(key, beforeExclusiveUtc, limit, closedOnly, ct);
        }

        public Task<DateTime?> GetLatestOpenTimeAsync(CandleKey key, CancellationToken ct) =>
            Inner.GetLatestOpenTimeAsync(key, ct);

        public Task<DateTime?> GetFirstMissingStatsOpenTimeAsync(CandleKey key, DateTime fromUtc, CancellationToken ct) =>
            Inner.GetFirstMissingStatsOpenTimeAsync(key, fromUtc, ct);

        public Task<UpsertResult> UpsertAsync(CandleKey key, IReadOnlyList<Candle> candles, CancellationToken ct) =>
            Inner.UpsertAsync(key, candles, ct);

        public Task<IReadOnlyList<Candle>> GetAfterAsync(CandleKey key, DateTime? afterExclusiveUtc, int limit,
            bool closedOnly, CancellationToken ct) =>
            Inner.GetAfterAsync(key, afterExclusiveUtc, limit, closedOnly, ct);
    }
}
