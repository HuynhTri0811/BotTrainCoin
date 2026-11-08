namespace CryptoBot.Domain.MarketData;

/// <summary>Một nến đáng chú ý trong báo cáo biến động.</summary>
/// <param name="OpenTimeUtc">Giờ mở của nến.</param>
/// <param name="Open">Giá mở.</param>
/// <param name="Close">Giá đóng.</param>
/// <param name="ChangePercent">% thay đổi từ giá mở đến giá đóng của nến.</param>
/// <param name="Volume">Khối lượng (coin).</param>
/// <param name="VolumeRatio">Khối lượng gấp bao nhiêu lần trung bình các nến trước; null khi chưa đủ nến để so.</param>
/// <param name="BuyPercent">Tỷ trọng mua chủ động (0–100); null khi nến chưa có khối lượng mua chủ động.</param>
public sealed record ReportCandle(
    DateTime OpenTimeUtc,
    decimal Open,
    decimal Close,
    decimal ChangePercent,
    decimal Volume,
    decimal? VolumeRatio,
    decimal? BuyPercent);
