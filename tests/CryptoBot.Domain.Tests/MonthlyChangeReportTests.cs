using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

public class MonthlyChangeReportTests
{
    private static Candle Day(int year, int month, int day, decimal open, decimal close, bool closed = true) =>
        new("Bitfinex", "BTCUSD", CandleInterval.D1, new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc),
            open, Math.Max(open, close), Math.Min(open, close), close, 1m, closed,
            new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Month_uses_first_day_open_and_last_day_close()
    {
        MonthlyChangeReport report = MonthlyChangeReport.Build([
            Day(2016, 1, 1, 100, 105), Day(2016, 1, 15, 105, 90), Day(2016, 1, 31, 90, 120),
            Day(2016, 2, 1, 120, 60),
        ]);

        MonthChange jan = report.Months[0];
        Assert.Equal((100m, 120m, 20m, 3), (jan.Open, jan.Close, jan.ChangePercent, jan.Days));
        Assert.True(jan.Complete);
        Assert.Equal(-50m, report.Months[1].ChangePercent);
    }

    [Fact]
    public void Running_last_month_is_incomplete_and_left_out_of_stats()
    {
        MonthlyChangeReport report = MonthlyChangeReport.Build([
            Day(2016, 3, 31, 100, 110), Day(2017, 3, 1, 100, 90), Day(2017, 3, 2, 90, 95, closed: false),
        ]);

        Assert.False(report.Months[^1].Complete);
        MonthOfYearStats march = Assert.Single(report.MonthStats);
        Assert.Equal((1, 10m, 1, 100m), (march.Years, march.AverageChangePercent, march.UpCount, march.UpPercent));
    }

    [Fact]
    public void Last_month_with_closed_last_calendar_day_is_complete()
    {
        MonthlyChangeReport report = MonthlyChangeReport.Build([Day(2016, 4, 1, 100, 100), Day(2016, 4, 30, 100, 80)]);

        Assert.True(Assert.Single(report.Months).Complete);
    }

    [Fact]
    public void Same_month_is_compared_across_years()
    {
        MonthlyChangeReport report = MonthlyChangeReport.Build([
            Day(2016, 5, 1, 100, 110), Day(2017, 5, 1, 100, 70), Day(2018, 5, 1, 100, 150), Day(2018, 6, 1, 1, 1),
        ]);

        MonthOfYearStats may = report.MonthStats.Single(stats => stats.Month == 5);
        Assert.Equal(3, may.Years);
        Assert.Equal(10m, may.AverageChangePercent);
        Assert.Equal((50m, -30m, 2), (may.BestChangePercent, may.WorstChangePercent, may.UpCount));
    }

    [Fact]
    public void Year_runs_from_first_month_open_to_last_month_close()
    {
        MonthlyChangeReport report = MonthlyChangeReport.Build([
            Day(2016, 7, 26, 2, 3), Day(2016, 12, 31, 3, 1), Day(2017, 1, 1, 1, 4),
        ]);

        YearChange y2016 = report.Years[0];
        Assert.Equal((2m, 1m, -50m, true), (y2016.Open, y2016.Close, y2016.ChangePercent, y2016.Complete));
        Assert.False(report.Years[1].Complete);
    }
}
