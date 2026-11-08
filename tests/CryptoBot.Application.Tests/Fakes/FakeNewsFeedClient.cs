using CryptoBot.Application.News;

namespace CryptoBot.Application.Tests;

internal sealed class FakeNewsFeedClient : INewsFeedClient
{
    public readonly Dictionary<string, List<NewsItemDto>> Feeds = [];
    public readonly HashSet<string> Failing = [];

    public Task<IReadOnlyList<NewsItemDto>> GetItemsAsync(string url, CancellationToken ct) =>
        Failing.Contains(url)
            ? throw new NewsFeedUnavailableException("503 Service Unavailable")
            : Task.FromResult<IReadOnlyList<NewsItemDto>>(Feeds.GetValueOrDefault(url) ?? []);
}
