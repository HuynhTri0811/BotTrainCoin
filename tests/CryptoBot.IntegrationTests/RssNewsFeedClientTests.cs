using System.Net;
using System.Text;
using CryptoBot.Application.News;
using CryptoBot.Infrastructure.News;

namespace CryptoBot.IntegrationTests;

/// <summary>Đọc XML thật qua HttpClient thật, chỉ thay phần mạng bằng một handler trả sẵn nội dung.</summary>
public sealed class RssNewsFeedClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/xml"),
            });
    }

    private static RssNewsFeedClient Client(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new HttpClient(new StubHandler(status, body)));

    [Fact]
    public async Task Reads_rss_items_and_skips_ones_without_title_or_date()
    {
        const string rss = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0"><channel>
              <item>
                <title>$6 billion Binance hack sends bitcoin, ether tumbling</title>
                <link>https://www.coindesk.com/markets/a</link>
                <guid isPermaLink="false">745dd8f6</guid>
                <pubDate>Fri, 30 Oct 2026 04:00:43 +0000</pubDate>
                <description><![CDATA[<p>Bitcoin and ether slid.</p>]]></description>
              </item>
              <item>
                <title>No guid uses the link</title>
                <link>https://decrypt.co/b</link>
                <pubDate>Thu, 29 Oct 2026 22:57:48 GMT</pubDate>
              </item>
              <item><title>No date</title><link>https://x/c</link></item>
              <item><link>https://x/d</link><pubDate>Fri, 30 Oct 2026 04:00:43 +0000</pubDate></item>
            </channel></rss>
            """;

        var items = await Client(rss).GetItemsAsync("https://feed.test/rss", default);

        Assert.Equal(2, items.Count);
        Assert.Equal("745dd8f6", items[0].Id);
        Assert.Equal(new DateTime(2026, 10, 30, 4, 0, 43, DateTimeKind.Utc), items[0].PublishedAtUtc);
        Assert.Equal(DateTimeKind.Utc, items[0].PublishedAtUtc.Kind);
        Assert.Equal("<p>Bitcoin and ether slid.</p>", items[0].Description);
        Assert.Equal("https://decrypt.co/b", items[1].Id);
        Assert.Equal(new DateTime(2026, 10, 29, 22, 57, 48, DateTimeKind.Utc), items[1].PublishedAtUtc);
    }

    [Fact]
    public async Task Reads_atom_entries()
    {
        const string atom = """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <entry>
                <id>tag:news.test,2026:1</id>
                <title>Ethereum upgrade goes live</title>
                <link rel="alternate" href="https://news.test/1"/>
                <published>2026-10-30T01:02:03Z</published>
                <summary>Short summary</summary>
              </entry>
            </feed>
            """;

        var item = Assert.Single(await Client(atom).GetItemsAsync("https://feed.test/atom", default));

        Assert.Equal("tag:news.test,2026:1", item.Id);
        Assert.Equal("https://news.test/1", item.Url);
        Assert.Equal(new DateTime(2026, 10, 30, 1, 2, 3, DateTimeKind.Utc), item.PublishedAtUtc);
    }

    [Fact]
    public async Task Http_error_and_broken_xml_are_feed_unavailable()
    {
        await Assert.ThrowsAsync<NewsFeedUnavailableException>(() =>
            Client("", HttpStatusCode.Forbidden).GetItemsAsync("https://feed.test/rss", default));
        await Assert.ThrowsAsync<NewsFeedUnavailableException>(() =>
            Client("<rss><channel><item>").GetItemsAsync("https://feed.test/rss", default));
    }
}
