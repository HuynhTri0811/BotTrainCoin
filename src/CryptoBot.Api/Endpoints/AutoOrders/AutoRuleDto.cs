namespace CryptoBot.Api.Endpoints;

public sealed record AutoRuleDto(long Id, string Environment, string Symbol, string Side, string TriggerType,
    string? Condition, decimal? TriggerPrice, string? IndicatorName, decimal Margin, int Leverage,
    decimal StopLossPercent, decimal? TakeProfitPercent, string Status, DateTime CreatedAtUtc,
    DateTime? TriggeredAtUtc, decimal? TriggeredMarkPrice, long? PositionId, string? Note, string? TriggerDetail,
    bool CloseOnReverse, DateTime? CloseWatchEndedAtUtc, string? CloseDetail);
