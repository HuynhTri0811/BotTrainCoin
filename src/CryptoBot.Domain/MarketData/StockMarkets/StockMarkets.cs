namespace CryptoBot.Domain.MarketData;

/// <summary>
/// Các sàn chứng khoán theo dõi giờ mở cửa, xếp theo giờ mở cửa trong ngày tính theo giờ Việt Nam.
/// Tạo danh sách không tra múi giờ, nên máy thiếu dữ liệu múi giờ chỉ lỗi khi tính giờ mở cửa.
/// </summary>
public static class StockMarkets
{
    /// <summary>Tokyo Stock Exchange, 09:00 giờ Tokyo (07:00 giờ Việt Nam). Nhật không đổi giờ mùa hè.</summary>
    public static readonly StockMarket Japan = new("JP", "Nhật", "TSE", "Asia/Tokyo", new TimeOnly(9, 0),
        StockMarketHolidays.Japan, 2027);

    /// <summary>Korea Exchange, 09:00 giờ Seoul (07:00 giờ Việt Nam). Hàn không đổi giờ mùa hè.</summary>
    public static readonly StockMarket Korea = new("KR", "Hàn", "KRX", "Asia/Seoul", new TimeOnly(9, 0),
        StockMarketHolidays.Korea, 2026);

    /// <summary>Sở Giao dịch Chứng khoán TP.HCM, phiên ATO lúc 09:00 giờ Việt Nam.</summary>
    public static readonly StockMarket Vietnam = new("VN", "Việt Nam", "HOSE", "Asia/Ho_Chi_Minh", new TimeOnly(9, 0),
        StockMarketHolidays.Vietnam, 2026);

    /// <summary>London Stock Exchange, 08:00 giờ London (14:00 giờ Việt Nam mùa hè, 15:00 mùa đông).</summary>
    public static readonly StockMarket UnitedKingdom = new("UK", "Anh", "LSE", "Europe/London", new TimeOnly(8, 0),
        StockMarketHolidays.UnitedKingdom, 2027);

    /// <summary>NYSE và Nasdaq, 09:30 giờ New York (20:30 giờ Việt Nam mùa hè, 21:30 mùa đông).</summary>
    public static readonly StockMarket UnitedStates = new("US", "Mỹ", "NYSE", "America/New_York", new TimeOnly(9, 30),
        StockMarketHolidays.UnitedStates, 2027);

    /// <summary>Tất cả các sàn, theo thứ tự trên.</summary>
    public static readonly IReadOnlyList<StockMarket> All = [Japan, Korea, Vietnam, UnitedKingdom, UnitedStates];
}
