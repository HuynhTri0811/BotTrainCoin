namespace CryptoBot.Api.Endpoints;

public sealed record IndicatorLineDto(string Key, string Title, string Color, bool Overlay, IReadOnlyList<decimal> Levels);
