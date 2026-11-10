namespace CryptoBot.Domain.MarketData;

/// <summary>Một sàn chứng khoán mở cửa từ Thứ Hai đến Thứ Sáu theo giờ địa phương.</summary>
/// <param name="Code">Mã quốc gia ngắn, ví dụ <c>US</c>, dùng làm khóa ở API và bộ lọc.</param>
/// <param name="Name">Tên quốc gia hiển thị, tiếng Việt.</param>
/// <param name="Exchange">Tên sàn, ví dụ <c>NYSE</c>.</param>
/// <param name="TimeZoneId">Múi giờ IANA của sàn, ví dụ <c>America/New_York</c>; giờ mùa hè tính theo múi giờ này.</param>
/// <param name="LocalOpen">Giờ mở cửa theo giờ địa phương của sàn.</param>
public sealed record StockMarket(string Code, string Name, string Exchange, string TimeZoneId, TimeOnly LocalOpen)
{
    /// <summary>Múi giờ của sàn; tìm một lần khi tạo, sai mã múi giờ thì lỗi ngay.</summary>
    public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
}
