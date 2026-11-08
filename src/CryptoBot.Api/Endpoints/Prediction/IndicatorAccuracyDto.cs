namespace CryptoBot.Api.Endpoints;

/// <summary>Tín hiệu Long (Up)/Short (Down) chấm trên Candles nến đã đóng gần nhất, bắt đầu từ FromTime.</summary>
public sealed record IndicatorAccuracyDto(string Name, string Type, int Candles, long? FromTime,
    IReadOnlyList<HorizonAccuracyDto> Horizons, IReadOnlyList<IndicatorSignalDto> Recent);
