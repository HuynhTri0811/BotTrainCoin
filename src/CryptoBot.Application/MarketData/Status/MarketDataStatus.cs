using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.MarketData;

public sealed record MarketDataStatus(
    string Key,
    DateTime? LastSuccessAtUtc,
    DateTime? LastAttemptAtUtc,
    string? LastError,
    int ConsecutiveFailures,
    DateTime? LatestClosedOpenTimeUtc,
    IReadOnlyList<CandleIssue> RecentIssues,
    bool IsStale);
