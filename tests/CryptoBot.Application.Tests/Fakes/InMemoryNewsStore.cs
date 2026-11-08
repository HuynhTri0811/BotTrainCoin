using CryptoBot.Application.News;
using CryptoBot.Domain.News;

namespace CryptoBot.Application.Tests;

internal sealed class InMemoryNewsStore : INewsStore
{
    public readonly List<NewsArticle> Articles = [];

    public Task<IReadOnlySet<string>> GetExistingIdsAsync(string source, IReadOnlyCollection<string> externalIds,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<string>>(Articles
            .Where(article => article.Source == source && externalIds.Contains(article.ExternalId))
            .Select(article => article.ExternalId).ToHashSet());

    public Task<IReadOnlyList<NewsArticle>> QueryAsync(NewsFilter filter, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NewsArticle>>(Articles
            .Where(article => filter.Coin is null || article.Coins.Split(',').Contains(filter.Coin))
            .Where(article => filter.Source is null || article.Source == filter.Source)
            .Where(article => filter.Search is null || article.Title.Contains(filter.Search)
                || article.Summary.Contains(filter.Search))
            .Where(article => (filter.FromUtc is null || article.PublishedAtUtc >= filter.FromUtc) &&
                        (filter.ToUtc is null || article.PublishedAtUtc < filter.ToUtc))
            .OrderByDescending(article => article.PublishedAtUtc).Take(limit).ToList());

    public void AddRange(IEnumerable<NewsArticle> articles) => Articles.AddRange(articles);

    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
}
