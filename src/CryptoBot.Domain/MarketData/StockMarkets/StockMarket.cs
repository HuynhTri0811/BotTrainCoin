namespace CryptoBot.Domain.MarketData;

/// <summary>Một sàn chứng khoán mở cửa từ Thứ Hai đến Thứ Sáu theo giờ địa phương, trừ ngày nghỉ lễ.</summary>
/// <param name="Code">Mã quốc gia ngắn, ví dụ <c>US</c>, dùng làm khóa ở API và bộ lọc.</param>
/// <param name="Name">Tên quốc gia hiển thị, tiếng Việt.</param>
/// <param name="Exchange">Tên sàn, ví dụ <c>NYSE</c>.</param>
/// <param name="TimeZoneId">Múi giờ IANA của sàn, ví dụ <c>America/New_York</c>; giờ mùa hè tính theo múi giờ này.</param>
/// <param name="LocalOpen">Giờ mở cửa theo giờ địa phương của sàn.</param>
/// <param name="Holidays">Ngày nghỉ lễ rơi vào Thứ Hai đến Thứ Sáu, theo ngày địa phương của sàn.</param>
/// <param name="HolidaysKnownThroughYear">Năm cuối cùng đã có đủ lịch nghỉ lễ; sau năm này mọi ngày thường đều tính là mở cửa.</param>
public sealed record StockMarket(string Code, string Name, string Exchange, string TimeZoneId, TimeOnly LocalOpen,
    IReadOnlySet<DateOnly> Holidays, int HolidaysKnownThroughYear)
{
    /// <summary>
    /// Múi giờ của sàn, tra theo <see cref="TimeZoneId"/> mỗi lần đọc (có cache theo mã), nên <c>with</c> đổi
    /// <see cref="TimeZoneId"/> thì múi giờ đổi theo. Ném <see cref="TimeZoneNotFoundException"/> nếu máy không có múi giờ này.
    /// </summary>
    public TimeZoneInfo TimeZone => TimeZoneCache.Find(TimeZoneId);

    /// <summary>Sàn có mở cửa vào ngày <paramref name="localDay"/> (ngày địa phương của sàn) không.</summary>
    public bool IsOpenOn(DateOnly localDay) =>
        localDay.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !Holidays.Contains(localDay);
}
