namespace CryptoBot.Application.LivePrices;

/// <summary>
/// Một lần lấy giá. Nếu một nguồn lỗi thì giá của nguồn đó là null và lý do nằm trong Error;
/// lỗi không bị che bằng giá cũ.
/// </summary>
public sealed record LivePriceSample(
    string Symbol,
    DateTime TimeUtc,
    decimal? SpotPrice,
    decimal? MarkPrice,
    int? SpotLatencyMs,
    int? MarkLatencyMs,
    string? Error);
