namespace CryptoBot.Api.Endpoints;

public sealed record IndicatorDto(string Name, string Type, string Description, string Symbol, string Interval,
    int RequiredCandles, IReadOnlyList<IndicatorLineDto> Lines);
