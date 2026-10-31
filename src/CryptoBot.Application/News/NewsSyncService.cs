using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.News;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.News;

/// <summary>Một nguồn tin RSS công khai (không cần API key).</summary>
public sealed class NewsFeedOptions
{
    /// <summary>Tên hiển thị và lưu cùng bài, ví dụ "CoinDesk". Đổi tên = nguồn mới.</summary>
    public string Name { get; set; } = "";

    public string Url { get; set; } = "";
}

public sealed class NewsOptions
{
    /// <summary>Chu kỳ đồng bộ tin. Feed chỉ giữ 20–40 bài mới nhất, nên chu kỳ quá dài có thể lỡ bài.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(5);

    /// <summary>Tóm tắt dài hơn bị cắt (chỉ lưu tóm tắt ngắn, đọc bài gốc qua link).</summary>
    public int MaxSummaryLength { get; set; } = 500;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);

    public List<NewsFeedOptions> Feeds { get; set; } = [];
}

/// <summary>Một bài trong feed, chưa xử lý.</summary>
/// <param name="Id">guid của bài, không có thì là link.</param>
/// <param name="Description">Tóm tắt, có thể chứa HTML.</param>
public sealed record NewsItemDto(string Id, string Title, string Url, string? Description, DateTime PublishedAtUtc);

public interface INewsFeedClient
{
    /// <summary>Các bài trong feed ở <paramref name="url"/>; bài thiếu tiêu đề hoặc ngày đăng bị bỏ qua.</summary>
    Task<IReadOnlyList<NewsItemDto>> GetItemsAsync(string url, CancellationToken ct);
}

/// <summary>Feed tạm thời không đọc được (lỗi mạng, 4xx/5xx, XML hỏng).</summary>
public sealed class NewsFeedUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Điều kiện lọc tin; trường null là không lọc. <see cref="Search"/> tìm trong tiêu đề và tóm tắt.</summary>
public sealed record NewsFilter(
    string? Coin = null,
    string? Source = null,
    string? Search = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);

public interface INewsStore
{
    /// <summary>Những mã trong <paramref name="externalIds"/> đã được lưu cho <paramref name="source"/>.</summary>
    Task<IReadOnlySet<string>> GetExistingIdsAsync(string source, IReadOnlyCollection<string> externalIds,
        CancellationToken ct);

    /// <summary>
    /// Tối đa <paramref name="limit"/> bài mới nhất (theo giờ đăng), mới → cũ, khớp <paramref name="filter"/>.
    /// Khoảng thời gian là [FromUtc, ToUtc).
    /// </summary>
    Task<IReadOnlyList<NewsArticle>> QueryAsync(NewsFilter filter, int limit, CancellationToken ct);

    void AddRange(IEnumerable<NewsArticle> articles);
    Task SaveAsync(CancellationToken ct);
}

/// <summary>Kết quả một lượt đồng bộ: số bài mới và lỗi theo nguồn.</summary>
public sealed record NewsSyncResult(int Added, IReadOnlyList<string> Errors);

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
