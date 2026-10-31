using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

public class SimilarDaysForecastTests
{
    /// <summary>Ngày lẻ tăng 1%, ngày chẵn giảm 1%: sau một ngày giảm, ngày kế tiếp luôn tăng.</summary>
    private static List<decimal> Alternating(int count)
    {
        List<decimal> closes = [100m];
        for (int index = 1; index < count; index++)
            closes.Add(closes[^1] * (index % 2 == 1 ? 1.01m : 0.99m));
        return closes;
    }

    [Fact]
    public void After_a_down_day_similar_days_point_up()
    {
        // Ngày cuối (chỉ số 600) là ngày giảm.
        SimilarDaysForecast forecast = SimilarDaysForecast.Compute(Alternating(601))!;

        Assert.Equal(100m, forecast.UpPercent);
        Assert.Equal(ForecastLean.Up, forecast.Lean);
        Assert.Equal(SimilarDaysForecast.DefaultNeighbors, forecast.Neighbors);
        Assert.InRange(forecast.MedianPercent, 0.999m, 1.001m);
    }

    [Fact]
    public void After_an_up_day_similar_days_point_down()
    {
        SimilarDaysForecast forecast = SimilarDaysForecast.Compute(Alternating(600))!;

        Assert.Equal(0m, forecast.UpPercent);
        Assert.Equal(ForecastLean.Down, forecast.Lean);
        Assert.True(forecast.Low68Percent <= forecast.MedianPercent
            && forecast.MedianPercent <= forecast.High68Percent);
    }

    [Fact]
    public void Too_little_history_returns_null()
    {
        Assert.Null(SimilarDaysForecast.Compute(Alternating(200)));
        // 250 giá: chỉ 49 ngày đã biết kết quả, ít hơn 60 ngày tương tự.
        Assert.Null(SimilarDaysForecast.Compute(Alternating(250)));
    }

    [Fact]
    public void Backtest_scores_a_perfectly_repeating_pattern()
    {
        SimilarDaysBacktest backtest = SimilarDaysForecast.Backtest(Alternating(1200), warmupDays: 100);

        Assert.True(backtest.Days > 800);
        Assert.Equal(100m, backtest.HitRatePercent);
        Assert.Equal(backtest.Days, backtest.StrongDays);
    }

    [Fact]
    public void Backtest_skips_days_without_a_direction()
    {
        // Giá đứng yên: không có hướng để chấm, nên không ngày nào được tính.
        List<decimal> flat = Enumerable.Repeat(100m, 1200).ToList();

        SimilarDaysBacktest backtest = SimilarDaysForecast.Backtest(flat, warmupDays: 100);

        Assert.Equal(0, backtest.Days);
        Assert.Equal(0m, backtest.HitRatePercent);
    }

    [Fact]
    public void Rejects_bad_input()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SimilarDaysForecast.Compute(Alternating(300), horizonDays: 0));
        Assert.Throws<ArgumentException>(() => SimilarDaysForecast.Compute([100m, 0m]));
    }
}
