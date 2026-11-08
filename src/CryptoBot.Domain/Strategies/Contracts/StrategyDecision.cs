namespace CryptoBot.Domain.Strategies;

public sealed record StrategyDecision(SignalAction Action, string Reason, IndicatorValues? Indicators)
{
    public static StrategyDecision Hold(string reason, IndicatorValues? indicators = null) =>
        new(SignalAction.Hold, reason, indicators);
}
