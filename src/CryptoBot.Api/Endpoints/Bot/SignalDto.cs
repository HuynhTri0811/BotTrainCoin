namespace CryptoBot.Api.Endpoints;

public sealed record SignalDto(long Id, int StrategyInstanceId, int ConfigVersion, string Symbol, string Interval,
    DateTime CandleOpenTimeUtc, long Time, string Action, string Reason, decimal CandleClose, decimal FastEma,
    decimal SlowEma, DateTime CreatedAtUtc, bool IsBackfill);
