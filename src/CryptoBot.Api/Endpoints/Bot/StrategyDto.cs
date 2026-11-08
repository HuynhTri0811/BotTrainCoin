using CryptoBot.Domain.Strategies;

namespace CryptoBot.Api.Endpoints;

public sealed record StrategyDto(int Id, string Name, string Type, int Version, string Exchange, string Symbol,
    string Interval, bool Enabled, DateTime? LastEvaluatedCandleOpenTimeUtc, EmaCrossParameters Parameters);
