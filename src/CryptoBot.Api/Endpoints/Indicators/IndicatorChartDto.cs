namespace CryptoBot.Api.Endpoints;

public sealed record IndicatorChartDto(IndicatorDto Indicator, IReadOnlyList<IndicatorPointDto> Points,
    IReadOnlyList<IndicatorMarkerDto> Markers, string? LatestReason);
