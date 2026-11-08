namespace CryptoBot.Api.Endpoints;

public sealed record StatusDto(string DecisionMode, string Environment, bool LiveLocked, DateTime ServerTimeUtc,
    IReadOnlyList<FeedStatusDto> Feeds, IReadOnlyList<StrategyDto> Strategies);
