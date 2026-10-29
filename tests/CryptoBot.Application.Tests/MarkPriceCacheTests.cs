using CryptoBot.Application.Futures;

namespace CryptoBot.Application.Tests;

public class MarkPriceCacheTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeClock _clock = new(T0);
    private readonly MarkPriceCache _cache;

    public MarkPriceCacheTests() =>
        _cache = new MarkPriceCache(_clock, new PaperFuturesOptions { MarkPriceMaxAge = TimeSpan.FromSeconds(10) });

    [Fact]
    public void Unknown_symbol_has_no_price()
    {
        Assert.Null(_cache.Get("BTCUSDT"));
        Assert.Null(_cache.GetFresh("BTCUSDT"));
    }

    [Fact]
    public void Price_is_fresh_up_to_max_age_then_only_available_as_stale()
    {
        _cache.Set(new MarkPrice("BTCUSDT", 100m, T0));

        _clock.UtcNow = T0.AddSeconds(10);
        Assert.Equal(100m, _cache.GetFresh("BTCUSDT")?.Price);

        _clock.UtcNow = T0.AddSeconds(10).AddMilliseconds(1);
        Assert.Null(_cache.GetFresh("BTCUSDT"));
        Assert.Equal(100m, _cache.Get("BTCUSDT")?.Price);
    }

    [Fact]
    public void Newer_price_replaces_older_one_per_symbol()
    {
        _cache.Set(new MarkPrice("BTCUSDT", 100m, T0));
        _cache.Set(new MarkPrice("ETHUSDT", 5m, T0));
        _cache.Set(new MarkPrice("BTCUSDT", 101m, T0.AddSeconds(2)));

        Assert.Equal(101m, _cache.Get("BTCUSDT")?.Price);
        Assert.Equal(5m, _cache.Get("ETHUSDT")?.Price);
    }

    [Fact]
    public void Symbol_rules_are_found_by_exact_symbol()
    {
        var options = new PaperFuturesOptions
        {
            Symbols = [new FuturesSymbolOptions { Symbol = "BTCUSDT", QuantityStep = 0.001m, TickSize = 0.1m,
                MinNotional = 50m, MaintenanceMarginRate = 0.004m }],
        };

        var rules = options.FindRules("BTCUSDT");

        Assert.NotNull(rules);
        Assert.Equal(0.001m, rules.QuantityStep);
        Assert.Equal(0.004m, rules.MaintenanceMarginRate);
        Assert.Null(options.FindRules("ETHUSDT"));
    }
}
