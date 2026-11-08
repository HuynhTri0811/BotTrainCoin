namespace CryptoBot.Api.Endpoints;

/// <summary>Time là Unix seconds (open time của nến đã đóng). Values: khóa = <see cref="IndicatorLineDto.Key"/>.</summary>
public sealed record IndicatorPointDto(long Time, IReadOnlyDictionary<string, decimal?> Values, string Signal);
