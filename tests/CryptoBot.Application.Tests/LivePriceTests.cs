using Microsoft.Extensions.Configuration;
using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Futures;
using CryptoBot.Application.LivePrices;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Tests;

public class LivePriceTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class StubSpot(Func<decimal> price) : IMarketDataClient
    {
        public string Exchange => "Binance";
        public Task<DateTime> GetServerTimeAsync(CancellationToken ct) => Task.FromResult(T0);
        public Task<decimal> GetLastPriceAsync(string symbol, CancellationToken ct) => Task.FromResult(price());
        public Task<IReadOnlyList<KlineDto>> GetKlinesAsync(string symbol, CandleInterval interval,
            DateTime startTimeUtc, int limit, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class StubMark(Func<decimal> price) : IMarkPriceClient
    {
        public Task<MarkPrice> GetMarkPriceAsync(string symbol, CancellationToken ct) =>
            Task.FromResult(new MarkPrice(symbol, price(), T0));
    }

    private static LivePriceSampler Sampler(IMarketDataClient spot, IMarkPriceClient mark, LivePriceHistory history) =>
        new(spot, mark, history, new FakeClock(T0));

    [Fact]
    public void Symbols_default_is_empty_so_configuration_does_not_create_duplicates()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Symbols:0"] = "BTCUSDT", ["Symbols:1"] = "ETHUSDT" })
            .Build();

        var options = config.Get<LivePriceOptions>()!;

        Assert.Equal(["BTCUSDT", "ETHUSDT"], options.Symbols);
    }

    [Fact]
    public async Task Sample_records_spot_and_mark_into_history()
    {
        var history = new LivePriceHistory(new LivePriceOptions());

        var s = await Sampler(new StubSpot(() => 100.5m), new StubMark(() => 100.7m), history)
            .SampleAsync("BTCUSDT", default);

        Assert.Equal(100.5m, s.SpotPrice);
        Assert.Equal(100.7m, s.MarkPrice);
        Assert.Null(s.Error);
        Assert.Equal(T0, s.TimeUtc);
        Assert.Single(history.Get("BTCUSDT", 10));
    }

    [Fact]
    public async Task One_source_failing_keeps_the_other_and_reports_error_without_fake_price()
    {
        var history = new LivePriceHistory(new LivePriceOptions());
        var failingMark = new StubMark(() => throw new MarketDataUnavailableException("rate limit"));

        var s = await Sampler(new StubSpot(() => 100m), failingMark, history).SampleAsync("BTCUSDT", default);

        Assert.Equal(100m, s.SpotPrice);
        Assert.Null(s.MarkPrice);
        Assert.Contains("Mark: rate limit", s.Error);
    }

    [Fact]
    public async Task History_keeps_only_the_newest_samples_in_order()
    {
        var history = new LivePriceHistory(new LivePriceOptions { HistorySize = 3 });
        var price = 0m;
        var sampler = Sampler(new StubSpot(() => ++price), new StubMark(() => price), history);

        for (var i = 0; i < 5; i++)
            await sampler.SampleAsync("BTCUSDT", default);

        Assert.Equal([3m, 4m, 5m], history.Get("BTCUSDT", 10).Select(s => s.SpotPrice!.Value));
        Assert.Equal([5m], history.Get("BTCUSDT", 1).Select(s => s.SpotPrice!.Value));
        Assert.Empty(history.Get("ETHUSDT", 10));
    }
}
