using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

public class SimilarDaysForecastTests
{
    /// <summary>Ngày lẻ tăng 1%, ngày chẵn giảm 1%: sau một ngày giảm, ngày kế tiếp luôn tăng.</summary>
    private static List<decimal> Alternating(int count)
    {
        List<decimal> closes = [100m];
        for (int i = 1; i < count; i++)
            closes.Add(closes[^1] * (i % 2 == 1 ? 1.01m : 0.99m));
        return closes;
    }

    [Fact]
    public void After_a_down_day_similar_days_point_up()
    {
        // Ngày cuối (chỉ số 600) là ngày giảm.
        SimilarDaysForecast f = SimilarDaysForecast.Compute(Alternating(601))!;

        Assert.Equal(100m, f.UpPercent);
        Assert.Equal(ForecastLean.Up, f.Lean);
        Assert.Equal(SimilarDaysForecast.DefaultNeighbors, f.Neighbors);
        Assert.InRange(f.MedianPercent, 0.999m, 1.001m);
    }

    [Fact]
    public void After_an_up_day_similar_days_point_down()
    {
        SimilarDaysForecast f = SimilarDaysForecast.Compute(Alternating(600))!;

        Assert.Equal(0m, f.UpPercent);
        Assert.Equal(ForecastLean.Down, f.Lean);
        Assert.True(f.Low68Percent <= f.MedianPercent && f.MedianPercent <= f.High68Percent);
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
        SimilarDaysBacktest b = SimilarDaysForecast.Backtest(Alternating(1200), warmupDays: 100);

        Assert.True(b.Days > 800);
        Assert.Equal(100m, b.HitRatePercent);
        Assert.Equal(b.Days, b.StrongDays);
    }

    [Fact]
    public void Rejects_bad_input()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SimilarDaysForecast.Compute(Alternating(300), horizonDays: 0));
        Assert.Throws<ArgumentException>(() => SimilarDaysForecast.Compute([100m, 0m]));
    }
}
