namespace CryptoBot.Api.Endpoints;

/// <summary>Move1/Move3: % giá đi theo hướng tín hiệu sau 1/3 nến; null khi chưa đủ nến.</summary>
public sealed record IndicatorSignalDto(long Time, string Direction, decimal Close, decimal? Move1, decimal? Move3,
    string Reason);
