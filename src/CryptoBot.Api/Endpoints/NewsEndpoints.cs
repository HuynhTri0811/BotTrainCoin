using CryptoBot.Application.News;
using CryptoBot.Domain.News;

namespace CryptoBot.Api.Endpoints;

public static class NewsEndpoints
{
    public static void MapNewsEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/news");

        // Tin đã đồng bộ (NewsWorker), mới → cũ; lọc theo coin và khoảng giờ đăng [fromUtc, toUtc).
        api.MapGet("", async (string? coin, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int? limit,
            INewsStore store, NewsOptions options, CancellationToken ct) =>
        {
            var picked = coin is null
                ? null
                : NewsText.Coins.FirstOrDefault(c => c.Equals(coin, StringComparison.OrdinalIgnoreCase));
            if (coin is not null && picked is null)
                return Results.BadRequest(new { error = $"Coin không hỗ trợ: {coin}. Chọn {string.Join(", ", NewsText.Coins)}." });

            var articles = await store.QueryAsync(picked, fromUtc?.UtcDateTime, toUtc?.UtcDateTime,
                Math.Clamp(limit ?? 50, 1, 200), ct);
            return Results.Ok(new NewsListDto(NewsText.Coins, options.Feeds.Select(f => f.Name).ToList(),
                options.PollInterval.TotalHours, articles.Select(ToDto).ToList()));
        });

        // Đồng bộ ngay, không chờ tới lượt kế tiếp của worker.
        api.MapPost("/sync", async (NewsSyncService svc, CancellationToken ct) =>
        {
            var result = await svc.SyncAsync(ct);
            return Results.Ok(new NewsSyncDto(result.Added, result.Errors));
        });
    }

    private static NewsArticleDto ToDto(NewsArticle a) =>
        new(a.Id, a.Source, a.Title, a.Url, a.Summary,
            a.Coins.Length == 0 ? [] : a.Coins.Split(','), a.PublishedAtUtc);
}

public sealed record NewsArticleDto(long Id, string Source, string Title, string Url, string Summary,
    IReadOnlyList<string> Coins, DateTime PublishedAtUtc);

public sealed record NewsListDto(IReadOnlyList<string> Coins, IReadOnlyList<string> Sources, double PollIntervalHours,
    IReadOnlyList<NewsArticleDto> Articles);

public sealed record NewsSyncDto(int Added, IReadOnlyList<string> Errors);
