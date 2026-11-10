namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Một sàn chứng khoán được đánh dấu giờ mở cửa. LocalOpen là giờ địa phương dạng <c>HH:mm</c>; NextOpenTime là Unix
/// seconds UTC của lần mở cửa kế tiếp tính từ nến được dự đoán, null nếu không tìm thấy.
/// </summary>
public sealed record StockMarketDto(string Code, string Name, string Exchange, string TimeZoneId, string LocalOpen,
    int HolidaysKnownThroughYear, long? NextOpenTime);
