using CryptoBot.Domain.News;

namespace CryptoBot.Domain.Tests;

public class NewsTextTests
{
    [Fact]
    public void Strips_tags_decodes_entities_and_collapses_spaces()
    {
        var html = "<p style=\"float:right\"><img src=\"x.jpg\"></p><p>Bitcoin &amp; ether\n   slid  after a hack.</p>";

        Assert.Equal("Bitcoin & ether slid after a hack.", NewsText.ToPlainText(html, 200));
    }

    [Fact]
    public void Cuts_long_text_with_an_ellipsis()
    {
        Assert.Equal("Bitcoin…", NewsText.ToPlainText("Bitcoin slid today", 9));
        Assert.Equal("", NewsText.ToPlainText(null, 10));
    }

    [Theory]
    [InlineData("Binance hack sends bitcoin, ether tumbling", "BTC,ETH")]
    [InlineData("BTC ETF inflows", "BTC")]
    [InlineData("Ethereum Classic network upgrade", "ETC")]
    [InlineData("ETC and ETH both fell", "ETH,ETC")]
    [InlineData("Stablecoins, tokens, etc. are under review", "")]
    [InlineData("Together with Bitcoiners", "")]
    public void Detects_coins_by_whole_word(string text, string expected)
    {
        Assert.Equal(expected, string.Join(',', NewsText.CoinsIn(text)));
    }

    [Fact]
    public void Article_rejects_missing_fields_and_local_times()
    {
        var utc = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);

        var article = new NewsArticle("CoinDesk", "id-1", "Title", "https://x", "", ["BTC", "ETH"], utc, utc);
        Assert.Equal("BTC,ETH", article.Coins);
        Assert.Throws<ArgumentException>(() => new NewsArticle("CoinDesk", "", "Title", "u", "", [], utc, utc));
        Assert.Throws<ArgumentException>(() =>
            new NewsArticle("CoinDesk", "id", "Title", "u", "", [], DateTime.SpecifyKind(utc, DateTimeKind.Local), utc));
    }
}
