namespace CryptoBot.Domain.MarketData;

/// <summary>Tìm các lần sàn chứng khoán mở cửa trong một khoảng thời gian, ví dụ trong một cây nến.</summary>
public static class MarketOpenings
{
    /// <summary>Nến dài từ mức này trở lên luôn chứa mọi phiên mở cửa trong ngày, nên không đánh dấu giờ mở cửa.</summary>
    public static readonly TimeSpan LongestCandle = TimeSpan.FromDays(1);

    /// <summary>Số ngày tìm tới khi tìm lần mở cửa kế tiếp, đủ vượt qua kỳ nghỉ dài nhất như Tết.</summary>
    private const int NextOpeningSearchDays = 21;

    /// <summary>Có đánh dấu giờ mở cửa cho nến dài <paramref name="candleDuration"/> không: chỉ nến ngắn hơn một ngày.</summary>
    public static bool AppliesTo(TimeSpan candleDuration) => candleDuration < LongestCandle;

    /// <summary>
    /// Các lần mở cửa trong [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), sắp theo thời gian; hai sàn mở
    /// cùng lúc giữ thứ tự của <paramref name="markets"/>. Ngày mở cửa xét theo giờ địa phương của sàn, không theo
    /// ngày UTC, và bỏ ngày nghỉ lễ đã biết.
    /// </summary>
    /// <param name="markets">Các sàn cần xét; mặc định <see cref="StockMarkets.All"/>.</param>
    /// <exception cref="TimeZoneNotFoundException">Máy không có dữ liệu múi giờ của một sàn.</exception>
    public static IReadOnlyList<MarketOpening> Between(DateTime fromUtc, DateTime toUtc,
        IReadOnlyList<StockMarket>? markets = null)
    {
        if (fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Thời gian phải là UTC.");
        ArgumentOutOfRangeException.ThrowIfLessThan(toUtc, fromUtc);

        List<MarketOpening> openings = [];
        foreach (StockMarket market in markets ?? StockMarkets.All)
        {
            TimeZoneInfo timeZone = market.TimeZone;
            DateOnly firstDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(fromUtc, timeZone));
            DateOnly lastDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(toUtc, timeZone));
            for (DateOnly day = firstDay; day <= lastDay; day = day.AddDays(1))
            {
                if (!market.IsOpenOn(day))
                    continue;

                DateTime openUtc = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(market.LocalOpen), timeZone);
                if (openUtc >= fromUtc && openUtc < toUtc)
                    openings.Add(new MarketOpening(market, openUtc));
            }
        }

        return openings.OrderBy(opening => opening.OpenUtc).ToList();
    }

    /// <summary>
    /// Các lần mở cửa trong [<paramref name="fromUtc"/>, <paramref name="toUtc"/>) chia theo nến chứa chúng, khóa là
    /// OpenTime của nến. Tính một lần cho cả khoảng thay vì từng nến. Nến dài từ <see cref="LongestCandle"/> trở lên
    /// trả về rỗng (xem <see cref="AppliesTo"/>).
    /// </summary>
    /// <exception cref="TimeZoneNotFoundException">Máy không có dữ liệu múi giờ của một sàn.</exception>
    public static ILookup<DateTime, MarketOpening> ByCandle(DateTime fromUtc, DateTime toUtc, CandleInterval interval,
        IReadOnlyList<StockMarket>? markets = null)
    {
        IReadOnlyList<MarketOpening> openings = AppliesTo(interval.Duration)
            ? Between(fromUtc, toUtc, markets)
            : [];
        return openings.ToLookup(opening => interval.OpenTimeAt(opening.OpenUtc));
    }

    /// <summary>
    /// Lần mở cửa đầu tiên của <paramref name="market"/> từ <paramref name="fromUtc"/> trở đi, tìm trong
    /// <see cref="NextOpeningSearchDays"/> ngày; null nếu không thấy.
    /// </summary>
    /// <exception cref="TimeZoneNotFoundException">Máy không có dữ liệu múi giờ của sàn.</exception>
    public static MarketOpening? Next(StockMarket market, DateTime fromUtc) =>
        Between(fromUtc, fromUtc.AddDays(NextOpeningSearchDays), [market]).FirstOrDefault();
}
