namespace CryptoBot.Api.Endpoints;

public sealed record FeedStatusDto(string Key, bool IsStale, DateTime? LastSuccessAtUtc, DateTime? LastAttemptAtUtc,
    string? LastError, int ConsecutiveFailures, DateTime? LatestClosedOpenTimeUtc, IReadOnlyList<string> RecentIssues);
