namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Time là Unix seconds (định dạng của Lightweight Charts). Số chỉ để hiển thị.
/// QuoteVolume (USDT), TradeCount và Pressure là null với nến lưu trước khi có các cột này.
/// </summary>
public sealed record ChartPointDto(long Time, decimal Open, decimal High, decimal Low, decimal Close,
    decimal Volume, decimal? QuoteVolume, long? TradeCount, bool IsClosed, decimal? FastEma, decimal? SlowEma,
    VolumePressureDto? Pressure);
