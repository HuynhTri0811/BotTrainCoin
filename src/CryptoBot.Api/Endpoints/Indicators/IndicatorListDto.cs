namespace CryptoBot.Api.Endpoints;

public sealed record IndicatorListDto(IReadOnlyList<IndicatorDto> Indicators, IReadOnlyList<IndicatorErrorDto> Errors,
    IReadOnlyList<string> AvailableTypes);
