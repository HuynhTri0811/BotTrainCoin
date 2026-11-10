namespace CryptoBot.Domain.MarketData;

/// <summary>Tìm các lần sàn chứng khoán mở cửa trong một khoảng thời gian, ví dụ trong một cây nến.</summary>
public static class MarketOpenings
{
    /// <summary>
    /// Các lần mở cửa trong [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), sắp theo thời gian; hai sàn mở
    /// cùng lúc giữ thứ tự của <paramref name="markets"/>. Ngày mở cửa là Thứ Hai đến Thứ Sáu theo giờ địa phương của
    /// sàn, không theo ngày UTC.
    /// </summary>
    /// <param name="markets">Các sàn cần xét; mặc định <see cref="StockMarkets.All"/>.</param>
    public static IReadOnlyList<MarketOpening> Between(DateTime fromUtc, DateTime toUtc,
        IReadOnlyList<StockMarket>? markets = null)
    {
        if (fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Thời gian phải là UTC.");
        ArgumentOutOfRangeException.ThrowIfLessThan(toUtc, fromUtc);

        List<MarketOpening> openings = [];
        foreach (StockMarket market in markets ?? StockMarkets.All)
        {
            DateOnly firstDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(fromUtc, market.TimeZone));
            DateOnly lastDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(toUtc, market.TimeZone));
            for (DateOnly day = firstDay; day <= lastDay; day = day.AddDays(1))
            {
                if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    continue;

                DateTime openUtc = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(market.LocalOpen), market.TimeZone);
                if (openUtc >= fromUtc && openUtc < toUtc)
                    openings.Add(new MarketOpening(market, openUtc));
            }
        }

        return openings.OrderBy(opening => opening.OpenUtc).ToList();
    }
}
