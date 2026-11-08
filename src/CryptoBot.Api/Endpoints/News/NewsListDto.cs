namespace CryptoBot.Api.Endpoints;

public sealed record NewsListDto(IReadOnlyList<string> Coins, IReadOnlyList<string> Sources, double PollIntervalHours,
    IReadOnlyList<NewsArticleDto> Articles);
