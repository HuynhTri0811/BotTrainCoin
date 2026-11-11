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
    public void With_a_new_time_zone_id_uses_the_new_time_zone()
    {
        // Chicago lệch New York một giờ: 09:30 Chicago = 15:30 UTC mùa đông.
        StockMarket chicago = StockMarkets.UnitedStates with { TimeZoneId = "America/Chicago" };

        Assert.Equal("America/Chicago", chicago.TimeZone.Id);
        Assert.Equal(Utc(2026, 11, 10, 15, 30),
            Assert.Single(MarketOpenings.Between(Utc(2026, 11, 10, 0), Utc(2026, 11, 11, 0), [chicago])).OpenUtc);
    }

    [Fact]
    public void Unknown_time_zone_fails_only_when_openings_are_computed()
    {
        StockMarket nowhere = StockMarkets.UnitedStates with { TimeZoneId = "Nowhere/Unknown" };

        Assert.Throws<TimeZoneNotFoundException>(() =>
            MarketOpenings.Between(Utc(2026, 11, 10, 0), Utc(2026, 11, 11, 0), [nowhere]));
        Assert.NotEmpty(MarketOpenings.Between(Utc(2026, 11, 10, 0), Utc(2026, 11, 11, 0)));
    }

    [Theory]
    [InlineData(15, true)]
    [InlineData(60, true)]
    [InlineData(1440, false)]
    [InlineData(10080, false)]
    public void Openings_apply_only_to_candles_shorter_than_a_day(int minutes, bool expected)
    {
        Assert.Equal(expected, MarketOpenings.AppliesTo(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void By_candle_groups_openings_under_the_candle_that_holds_them()
    {
        ILookup<DateTime, MarketOpening> byCandle =
            MarketOpenings.ByCandle(Utc(2026, 11, 10, 0), Utc(2026, 11, 11, 0), CandleInterval.H1);

        Assert.Equal(["JP", "KR"], byCandle[Utc(2026, 11, 10, 0)].Select(opening => opening.Market.Code));
        Assert.Equal(["VN"], byCandle[Utc(2026, 11, 10, 2)].Select(opening => opening.Market.Code));
        Assert.Equal(["UK"], byCandle[Utc(2026, 11, 10, 8)].Select(opening => opening.Market.Code));
        Assert.Equal(["US"], byCandle[Utc(2026, 11, 10, 14)].Select(opening => opening.Market.Code));
        Assert.Empty(byCandle[Utc(2026, 11, 10, 13)]);
        Assert.Equal(4, byCandle.Count);
    }

    [Fact]
    public void By_candle_puts_the_us_open_in_the_14_30_quarter_hour()
    {
        ILookup<DateTime, MarketOpening> byCandle =
            MarketOpenings.ByCandle(Utc(2026, 11, 10, 14), Utc(2026, 11, 10, 15), CandleInterval.M15);

        Assert.Equal(["US"], byCandle[Utc(2026, 11, 10, 14, 30)].Select(opening => opening.Market.Code));
    }

    [Fact]
    public void By_candle_is_empty_for_daily_candles()
    {
        Assert.Equal(0, MarketOpenings.ByCandle(Utc(2026, 11, 1, 0), Utc(2026, 11, 30, 0), CandleInterval.D1).Count);
    }

    [Fact]
    public void Exchanges_stay_closed_on_their_holidays()
    {
        // Lễ Tạ ơn Mỹ 26/11/2026: các sàn khác vẫn mở.
        Assert.Equal(["JP", "KR", "VN", "UK"], CodesIn(Utc(2026, 11, 26, 0), Utc(2026, 11, 27, 0)));
        // Giáng sinh 25/12/2026: Mỹ, Anh, Hàn nghỉ; Nhật và Việt Nam mở.
        Assert.Equal(["JP", "VN"], CodesIn(Utc(2026, 12, 25, 0), Utc(2026, 12, 26, 0)));
    }

    [Fact]
    public void Every_listed_holiday_is_a_weekday()
    {
        foreach (StockMarket market in StockMarkets.All)
            Assert.All(market.Holidays, holiday =>
                Assert.DoesNotContain(holiday.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }));
    }

    [Fact]
    public void Holidays_stay_within_the_known_years()
    {
        foreach (StockMarket market in StockMarkets.All)
            Assert.All(market.Holidays, holiday => Assert.InRange(holiday.Year, 2026, market.HolidaysKnownThroughYear));
    }

    [Fact]
    public void Next_opening_skips_the_weekend()
    {
        // Sau phiên Thứ Sáu 27/11/2026, Mỹ mở lại Thứ Hai 30/11 lúc 14:30 UTC.
        MarketOpening? next = MarketOpenings.Next(StockMarkets.UnitedStates, Utc(2026, 11, 27, 15));

        Assert.Equal(Utc(2026, 11, 30, 14, 30), next?.OpenUtc);
    }

    [Fact]
    public void Next_opening_skips_a_long_holiday()
    {
        // Tết 2026: HOSE nghỉ 16/02 đến 20/02, mở lại Thứ Hai 23/02 lúc 02:00 UTC.
        MarketOpening? next = MarketOpenings.Next(StockMarkets.Vietnam, Utc(2026, 2, 13, 3));

        Assert.Equal(Utc(2026, 2, 23, 2), next?.OpenUtc);
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
