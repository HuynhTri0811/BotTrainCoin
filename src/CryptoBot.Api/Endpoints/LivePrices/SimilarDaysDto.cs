namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Dự báo "ngày tương tự" (SimilarDaysForecast) cho giá đóng cửa lúc TargetCloseUtc, kèm tỷ lệ đúng khi chạy thử.
/// Low68/High68 là giá: AnchorPrice (giá đóng hôm qua) × (1 + % của các ngày tương tự). Lean: Up / Down / Neutral.
/// </summary>
public sealed record SimilarDaysDto(string HistorySymbol, string Exchange, DateTime AsOfDayUtc, decimal AnchorPrice,
    DateTime TargetCloseUtc, int Neighbors, decimal UpPercent, decimal MedianPercent, decimal Low68Percent,
    decimal High68Percent, decimal Low68, decimal High68, string Lean, int TestedDays, decimal HitRatePercent,
    int StrongDays, decimal StrongHitRatePercent);
