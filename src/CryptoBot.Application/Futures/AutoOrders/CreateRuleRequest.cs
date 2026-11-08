using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public sealed record CreateRuleRequest(
    string Symbol,
    PositionSide Side,
    TriggerCondition? Condition,
    decimal? TriggerPrice,
    decimal Margin,
    int Leverage,
    decimal StopLossPercent,
    decimal? TakeProfitPercent,
    RuleTriggerType TriggerType = RuleTriggerType.Price,
    string? IndicatorName = null,
    bool CloseOnReverse = false);
