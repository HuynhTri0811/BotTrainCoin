using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using CryptoBot.Application.News;

namespace CryptoBot.Infrastructure.News;

/// <summary>
/// Đọc feed RSS 2.0 (<c>channel/item</c>) hoặc Atom (<c>feed/entry</c>) công khai. Bài thiếu tiêu đề hoặc ngày đăng
/// đọc được thì bị bỏ qua thay vì làm hỏng cả feed.
/// </summary>
public sealed class RssNewsFeedClient(HttpClient http) : INewsFeedClient
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public async Task<IReadOnlyList<NewsItemDto>> GetItemsAsync(string url, CancellationToken ct)
    {
        XDocument doc;
        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                throw new NewsFeedUnavailableException($"{(int)response.StatusCode} {response.ReasonPhrase}");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException
                                       or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new NewsFeedUnavailableException($"Không đọc được feed: {ex.Message}", ex);
        }

        var rss = doc.Root?.Element("channel")?.Elements("item").Select(FromRss);
        var atom = doc.Root?.Elements(Atom + "entry").Select(FromAtom);
        return (rss ?? atom ?? []).OfType<NewsItemDto>().ToList();
    }

    private static NewsItemDto? FromRss(XElement item)
    {
        var title = item.Element("title")?.Value;
        var link = item.Element("link")?.Value.Trim() ?? "";
        var id = item.Element("guid")?.Value.Trim() is { Length: > 0 } guid ? guid : link;
        return Create(id, title, link, item.Element("description")?.Value, item.Element("pubDate")?.Value);
    }

    private static NewsItemDto? FromAtom(XElement entry)
    {
        var link = entry.Elements(Atom + "link")
            .FirstOrDefault(l => (string?)l.Attribute("rel") is null or "alternate")?.Attribute("href")?.Value ?? "";
        return Create(entry.Element(Atom + "id")?.Value.Trim() ?? link, entry.Element(Atom + "title")?.Value, link,
            entry.Element(Atom + "summary")?.Value,
            entry.Element(Atom + "published")?.Value ?? entry.Element(Atom + "updated")?.Value);
    }

    private static NewsItemDto? Create(string id, string? title, string link, string? description, string? published)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) ||
            !DateTimeOffset.TryParse(published, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                out var at))
            return null;
        return new NewsItemDto(id, title.Trim(), link, description, at.UtcDateTime);
    }
}
