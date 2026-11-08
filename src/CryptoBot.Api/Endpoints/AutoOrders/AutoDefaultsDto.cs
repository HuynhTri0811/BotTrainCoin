namespace CryptoBot.Api.Endpoints;

public sealed record AutoDefaultsDto(decimal Margin, int Leverage, decimal StopLossPercent, decimal TakeProfitPercent,
    decimal DailyLossLimitPercent, int MaxActiveRules);
