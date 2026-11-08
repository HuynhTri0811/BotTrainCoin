using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.News;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.News;

/// <summary>
/// Lấy bài mới từ mọi nguồn RSS trong cấu hình và lưu những bài chưa có. Một nguồn lỗi không làm dừng các nguồn khác.
/// </summary>
public sealed class NewsSyncService(
    INewsFeedClient client,
    INewsStore store,
    IClock clock,
    NewsOptions options,
    ILogger<NewsSyncService> logger)
{
    public async Task<NewsSyncResult> SyncAsync(CancellationToken ct)
    {
        var added = 0;
        var errors = new List<string>();
        foreach (var feed in options.Feeds)
        {
            try
            {
                added += await SyncFeedAsync(feed, ct);
            }
            catch (NewsFeedUnavailableException ex)
            {
                logger.LogWarning("News {Source}: {Error}", feed.Name, ex.Message);
                errors.Add($"{feed.Name}: {ex.Message}");
            }
        }

        if (added > 0)
            logger.LogInformation("News: +{Added} bài", added);
        return new NewsSyncResult(added, errors);
    }

    private async Task<int> SyncFeedAsync(NewsFeedOptions feed, CancellationToken ct)
    {
        var items = (await client.GetItemsAsync(feed.Url, ct)).DistinctBy(item => item.Id).ToList();
        var existing = await store.GetExistingIdsAsync(feed.Name, items.Select(item => item.Id).ToList(), ct);
        var now = clock.UtcNow;
        var articles = items
            .Where(item => !existing.Contains(item.Id))
            .Select(item =>
            {
                var summary = NewsText.ToPlainText(item.Description, options.MaxSummaryLength);
                var title = NewsText.ToPlainText(item.Title, 500);
                return new NewsArticle(feed.Name, item.Id, title, item.Url, summary,
                    NewsText.CoinsIn($"{title} {summary}"),
                    item.PublishedAtUtc, now);
            })
            .ToList();

        if (articles.Count == 0)
            return 0;
        store.AddRange(articles);
        await store.SaveAsync(ct);
        return articles.Count;
    }
}
