using CryptoBot.Application.LivePrices;
using CryptoBot.Application.MarketData;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Tests;

public class SimilarDaysServiceTests
{
    private static readonly DateTime D0 = new(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryCandleStore _store = new();
    private readonly SimilarDaysCache _cache = new();

    private SimilarDaysService CreateService() =>
        new(_store, new FakeDailyHistoryClient(), new DailyHistoryOptions { Symbols = ["BTCUSD", "ETHUSD"] }, _cache);

    private void AddAlternatingDays(int count)
    {
        var close = 100m;
        for (var i = 0; i < count; i++)
        {
            var open = close;
            close = i == 0 ? 100m : close * (i % 2 == 1 ? 1.01m : 0.99m);
            var t = D0.AddDays(i);
            _store.Candles[t] = new Candle("Bitfinex", "BTCUSD", CandleInterval.D1, t, open,
                Math.Max(open, close), Math.Min(open, close), close, 1, isClosed: true, receivedAtUtc: t);
        }
    }

    [Theory]
    [InlineData("BTCUSDT", "BTCUSD")]
    [InlineData("ethusdt", "ETHUSD")]
    [InlineData("BTCUSD", "BTCUSD")]
    [InlineData("SOLUSDT", null)]
    public void Maps_live_symbol_to_history_symbol(string live, string? expected) =>
        Assert.Equal(expected, CreateService().ToHistorySymbol(live));

    [Fact]
    public async Task Forecasts_from_stored_history_and_caches_backtest()
    {
        AddAlternatingDays(1000);

        var result = await CreateService().GetAsync("BTCUSDT", 1, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(("BTCUSD", "Bitfinex"), (result.HistorySymbol, result.Exchange));
        Assert.Equal(D0.AddDays(999), result.AsOfDayUtc);
        Assert.Equal(ForecastLean.Down, result.Forecast.Lean);
        Assert.Single(_cache.Backtests);
    }

    [Fact]
    public async Task Unknown_symbol_or_empty_history_returns_null()
    {
        Assert.Null(await CreateService().GetAsync("SOLUSDT", 1, CancellationToken.None));
        Assert.Null(await CreateService().GetAsync("BTCUSDT", 1, CancellationToken.None));
    }
}
