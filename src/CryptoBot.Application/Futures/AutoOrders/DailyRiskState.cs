namespace CryptoBot.Application.Futures;

public sealed record DailyRiskState(
    DateTime DayStartUtc,
    decimal DayStartEquity,
    decimal TodayRealizedPnl,
    decimal LossLimit,
    bool Blocked);
