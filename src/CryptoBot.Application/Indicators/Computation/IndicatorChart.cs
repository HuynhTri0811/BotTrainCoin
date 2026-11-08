namespace CryptoBot.Application.Indicators;

public sealed record IndicatorChart(ConfiguredIndicator Indicator, IReadOnlyList<IndicatorPoint> Points);
