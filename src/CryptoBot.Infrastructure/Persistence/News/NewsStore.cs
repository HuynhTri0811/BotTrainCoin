using CryptoBot.Application.News;
using CryptoBot.Domain.News;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.Infrastructure.Persistence;

public sealed class NewsStore(BotDbContext db) : INewsStore
{
    public async Task<IReadOnlySet<string>> GetExistingIdsAsync(string source, IReadOnlyCollection<string> externalIds,
        CancellationToken ct) =>
        (await db.NewsArticles
            .Where(article => article.Source == source && externalIds.Contains(article.ExternalId))
            .Select(article => article.ExternalId).ToListAsync(ct)).ToHashSet();

    public async Task<IReadOnlyList<NewsArticle>> QueryAsync(NewsFilter filter, int limit, CancellationToken ct)
    {
        var query = db.NewsArticles.AsNoTracking();
        // Coins là "BTC,ETH": mã coin không trùng một phần với nhau nên tìm chuỗi con là đủ.
        if (filter.Coin is { } coin)
            query = query.Where(article => article.Coins.Contains(coin));
        if (filter.Source is { } source)
            query = query.Where(article => article.Source == source);
        if (filter.Search is { } search)
        {
            // Giữ nguyên %, _ người dùng gõ. LIKE của SQLite chỉ không phân biệt hoa thường với ký tự ASCII.
            var pattern = $"%{search.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
            query = query.Where(article => EF.Functions.Like(article.Title, pattern, @"\") ||
                                     EF.Functions.Like(article.Summary, pattern, @"\"));
        }
        if (filter.FromUtc is { } from)
            query = query.Where(article => article.PublishedAtUtc >= from);
        if (filter.ToUtc is { } to)
            query = query.Where(article => article.PublishedAtUtc < to);
        return await query.OrderByDescending(article => article.PublishedAtUtc).ThenByDescending(article => article.Id)
            .Take(limit).ToListAsync(ct);
    }

    public void AddRange(IEnumerable<NewsArticle> articles) => db.NewsArticles.AddRange(articles);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
