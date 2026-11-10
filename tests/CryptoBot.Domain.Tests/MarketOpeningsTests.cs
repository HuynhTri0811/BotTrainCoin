using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

public class MarketOpeningsTests
{
    private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static IReadOnlyList<string> CodesIn(DateTime fromUtc, DateTime toUtc) =>
        MarketOpenings.Between(fromUtc, toUtc).Select(opening => opening.Market.Code).ToList();

    [Fact]
    public void United_States_opens_at_14_30_utc_in_winter()
    {
        // Thứ Ba 10/11/2026, New York đã về giờ mùa đông (UTC-5): 09:30 = 14:30 UTC = 21:30 giờ Việt Nam.
        IReadOnlyList<MarketOpening> openings = MarketOpenings.Between(Utc(2026, 11, 10, 14), Utc(2026, 11, 10, 15));

        MarketOpening opening = Assert.Single(openings);
        Assert.Equal(StockMarkets.UnitedStates, opening.Market);
        Assert.Equal(Utc(2026, 11, 10, 14, 30), opening.OpenUtc);
    }

    [Fact]
    public void United_States_opens_at_13_30_utc_in_summer()
    {
        IReadOnlyList<MarketOpening> openings = MarketOpenings.Between(Utc(2026, 7, 14, 13), Utc(2026, 7, 14, 14));

        Assert.Equal(Utc(2026, 7, 14, 13, 30), Assert.Single(openings).OpenUtc);
    }

    [Fact]
    public void United_States_moves_to_summer_time_on_the_first_monday_after_the_switch()
    {
        // Mỹ đổi sang giờ mùa hè Chủ nhật 08/03/2026; Thứ Hai đã mở cửa lúc 13:30 UTC.
        IReadOnlyList<MarketOpening> openings =
            MarketOpenings.Between(Utc(2026, 3, 9, 0), Utc(2026, 3, 10, 0), [StockMarkets.UnitedStates]);

        Assert.Equal(Utc(2026, 3, 9, 13, 30), Assert.Single(openings).OpenUtc);
    }

    [Theory]
    [InlineData(1, 15, 8)] // Mùa đông: London = UTC.
    [InlineData(7, 15, 7)] // Mùa hè: London = UTC+1.
    public void United_Kingdom_opens_at_08_00_london_time(int month, int day, int expectedUtcHour)
    {
        IReadOnlyList<MarketOpening> openings =
            MarketOpenings.Between(Utc(2026, month, day, 0), Utc(2026, month, day + 1, 0), [StockMarkets.UnitedKingdom]);

        Assert.Equal(Utc(2026, month, day, expectedUtcHour), Assert.Single(openings).OpenUtc);
    }

    [Fact]
    public void Asian_markets_open_in_the_vietnamese_morning()
    {
        // Thứ Ba 10/11/2026: Nhật và Hàn 00:00 UTC (07:00 VN), Việt Nam 02:00 UTC (09:00 VN).
        IReadOnlyList<MarketOpening> openings = MarketOpenings.Between(Utc(2026, 11, 10, 0), Utc(2026, 11, 10, 3));

        Assert.Equal(["JP", "KR", "VN"], openings.Select(opening => opening.Market.Code));
        Assert.Equal(
            [Utc(2026, 11, 10, 0), Utc(2026, 11, 10, 0), Utc(2026, 11, 10, 2)],
            openings.Select(opening => opening.OpenUtc));
    }

    [Fact]
    public void A_weekday_has_every_market_opening_once_in_time_order()
    {
        Assert.Equal(["JP", "KR", "VN", "UK", "US"], CodesIn(Utc(2026, 11, 10, 0), Utc(2026, 11, 11, 0)));
    }

    [Fact]
    public void Saturday_has_no_openings()
    {
        Assert.Empty(CodesIn(Utc(2026, 11, 14, 0), Utc(2026, 11, 15, 0)));
    }

    [Fact]
    public void Japan_and_korea_open_the_week_at_monday_midnight_utc()
    {
        // 00:00 UTC Chủ nhật 15/11 là 09:00 Chủ nhật ở Tokyo và Seoul: không mở cửa.
        // 00:00 UTC Thứ Hai 16/11 là 09:00 Thứ Hai: mở cửa.
        Assert.Empty(CodesIn(Utc(2026, 11, 15, 0), Utc(2026, 11, 15, 1)));
        Assert.Equal(["JP", "KR"], CodesIn(Utc(2026, 11, 16, 0), Utc(2026, 11, 16, 1)));
    }

    [Fact]
    public void End_of_the_range_is_excluded()
    {
        Assert.Empty(CodesIn(Utc(2026, 11, 10, 14), Utc(2026, 11, 10, 14, 30)));
        Assert.Equal(["US"], CodesIn(Utc(2026, 11, 10, 14, 30), Utc(2026, 11, 10, 14, 31)));
    }

    [Fact]
    public void Non_utc_times_are_rejected()
    {
        DateTime local = new(2026, 11, 10, 14, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => MarketOpenings.Between(local, Utc(2026, 11, 10, 15)));
    }

    [Fact]
    public void End_before_start_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MarketOpenings.Between(Utc(2026, 11, 10, 15), Utc(2026, 11, 10, 14)));
    }
}
