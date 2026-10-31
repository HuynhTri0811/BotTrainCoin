using CryptoBot.Application.News;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.Application.Tests;

public class NewsSyncServiceTests
{
    private static readonly DateTime T0 = new(2026, 10, 30, 4, 0, 0, DateTimeKind.Utc);
    private const string CoinDesk = "https://coindesk.test/rss";
    private const string Decrypt = "https://decrypt.test/feed";

    private readonly FakeClock _clock = new(T0);
    private readonly FakeNewsFeedClient _client = new();
    private readonly InMemoryNewsStore _store = new();
    private readonly NewsSyncService _service;

    public NewsSyncServiceTests()
    {
        _service = new NewsSyncService(_client, _store, _clock,
            new NewsOptions
            {
                MaxSummaryLength = 40,
                Feeds = [new NewsFeedOptions { Name = "CoinDesk", Url = CoinDesk },
                    new NewsFeedOptions { Name = "Decrypt", Url = Decrypt }],
            },
            NullLogger<NewsSyncService>.Instance);
    }

    private static NewsItemDto Item(string id, string title, string? description = null, int hoursAgo = 1) =>
        new(id, title, $"https://news.test/{id}", description, T0.AddHours(-hoursAgo));

    [Fact]
    public void Default_poll_interval_is_5_hours()
    {
        Assert.Equal(TimeSpan.FromHours(5), new NewsOptions().PollInterval);
    }

    [Fact]
    public async Task Stores_new_articles_with_plain_text_summary_and_coins()
    {
        _client.Feeds[CoinDesk] =
        [
            Item("a1", "$6 billion Binance hack sends bitcoin, ether tumbling",
                "<p>Bitcoin &amp; ether slid after a suspected security breach at the exchange.</p>"),
        ];

        var result = await _service.SyncAsync(default);

        Assert.Equal(1, result.Added);
        var article = Assert.Single(_store.Articles);
        Assert.Equal("CoinDesk", article.Source);
        Assert.Equal("a1", article.ExternalId);
        Assert.Equal("BTC,ETH", article.Coins);
        Assert.Equal("Bitcoin & ether slid after a suspected…", article.Summary);
        Assert.Equal(T0.AddHours(-1), article.PublishedAtUtc);
        Assert.Equal(T0, article.FetchedAtUtc);
    }

    [Fact]
    public async Task Syncing_again_only_adds_articles_not_seen_before()
    {
        _client.Feeds[CoinDesk] = [Item("a1", "First")];
        await _service.SyncAsync(default);

        _client.Feeds[CoinDesk] = [Item("a2", "Second"), Item("a1", "First"), Item("a2", "Second")];
        var result = await _service.SyncAsync(default);

        Assert.Equal(1, result.Added);
        Assert.Equal(["a1", "a2"], _store.Articles.Select(article => article.ExternalId));
    }

    [Fact]
    public async Task Same_id_from_different_sources_is_kept_separately()
    {
        _client.Feeds[CoinDesk] = [Item("x", "From CoinDesk")];
        _client.Feeds[Decrypt] = [Item("x", "From Decrypt")];

        Assert.Equal(2, (await _service.SyncAsync(default)).Added);
    }

    [Fact]
    public async Task Failing_feed_is_reported_without_stopping_the_others()
    {
        _client.Failing.Add(CoinDesk);
        _client.Feeds[Decrypt] = [Item("d1", "Ethereum upgrade")];

        var result = await _service.SyncAsync(default);

        Assert.Equal(1, result.Added);
        Assert.StartsWith("CoinDesk:", Assert.Single(result.Errors));
        Assert.Equal("Decrypt", Assert.Single(_store.Articles).Source);
    }
}
