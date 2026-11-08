namespace CryptoBot.Api.Endpoints;

public sealed record NewsArticleDto(long Id, string Source, string Title, string Url, string Summary,
    IReadOnlyList<string> Coins, DateTime PublishedAtUtc);
