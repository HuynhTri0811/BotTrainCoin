namespace CryptoBot.Api.Endpoints;

/// <summary>
/// TriggerType: "Price" (mặc định, cần Condition + TriggerPrice) hoặc "Indicator" (cần IndicatorName).
/// CloseOnReverse: chỉ với "Indicator", tự đóng vị thế khi chỉ báo phát tín hiệu ngược hướng.
/// </summary>
public sealed record CreateRuleDto(string Symbol, string Side, string? Condition, decimal? TriggerPrice, decimal Margin,
    int Leverage, decimal StopLossPercent, decimal? TakeProfitPercent, string? TriggerType = null,
    string? IndicatorName = null, bool CloseOnReverse = false);
