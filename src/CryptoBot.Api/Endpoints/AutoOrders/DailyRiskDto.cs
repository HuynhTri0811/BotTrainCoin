namespace CryptoBot.Api.Endpoints;

public sealed record DailyRiskDto(DateTime DayStartUtc, decimal DayStartEquity, decimal TodayRealizedPnl,
    decimal LossLimit, bool Blocked);
