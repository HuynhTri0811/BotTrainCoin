using CryptoBot.Application.MarketData;

namespace CryptoBot.Application.Tests;

public class MarketEventCatalogTests
{
    private static MarketEvent Event(string month, string title, params string[] symbols) =>
        new() { Month = month, Title = title, Symbols = [.. symbols] };

    [Fact]
    public void Returns_market_wide_and_matching_symbol_events_in_month_order()
    {
        var catalog = new MarketEventCatalog([
            Event("2022-05", "Terra"),
            Event("2019-01", "ETC 51%", "ETCUSD"),
            Event("2019-01", "Constantinople", "ETHUSD"),
        ]);

        var etc = catalog.For("etcusd");

        Assert.Equal(["ETC 51%", "Terra"], etc.Select(e => e.Title));
        Assert.Equal((2019, 1, false), (etc[0].Year, etc[0].Month, etc[0].MarketWide));
        Assert.True(etc[1].MarketWide);
    }

    [Theory]
    [InlineData("2022-5")]
    [InlineData("2022/05")]
    [InlineData("")]
    public void Rejects_bad_month(string month)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new MarketEventCatalog([Event(month, "x")]));
        Assert.Contains("yyyy-MM", ex.Message);
    }

    [Fact]
    public void Rejects_missing_title() =>
        Assert.Throws<InvalidOperationException>(() => new MarketEventCatalog([Event("2022-05", " ")]));
}
