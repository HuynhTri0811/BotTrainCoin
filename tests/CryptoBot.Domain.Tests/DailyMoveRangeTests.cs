using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

public class DailyMoveRangeTests
{
    /// <summary>Giá xen kẽ +2% / −2% mỗi ngày.</summary>
    private static List<decimal> Alternating(int days)
    {
        List<decimal> closes = [100m];
        for (int index = 0; index < days; index++)
            closes.Add(closes[^1] * (index % 2 == 0 ? 1.02m : 0.98m));
        return closes;
    }

    [Fact]
    public void Range_is_symmetric_around_price_and_95_wider_than_68()
    {
        DailyMoveRange range = DailyMoveRange.Compute(Alternating(30), 1000)!;

        Assert.Equal(30, range.Days);
        Assert.True(range.Low95 < range.Low68 && range.Low68 < 1000 && 1000 < range.High68
            && range.High68 < range.High95);
        // Nhân/chia cùng một hệ số: thấp × cao = giá².
        Assert.Equal(1000m * 1000m, Math.Round(range.Low68 * range.High68, 6));
    }

    [Fact]
    public void Typical_move_matches_daily_swing()
    {
        DailyMoveRange range = DailyMoveRange.Compute(Alternating(30), 100)!;

        Assert.InRange(range.TypicalMovePercent, 1.9m, 2.1m);
        Assert.InRange(range.AverageMovePercent, 1.99m, 2.01m);
        Assert.Equal(2m, Math.Round(range.LargestRisePercent, 6));
        Assert.Equal(-2m, Math.Round(range.LargestDropPercent, 6));
        Assert.Equal(50m, range.UpDaysPercent);
    }

    [Fact]
    public void Flat_prices_have_zero_range()
    {
        DailyMoveRange range = DailyMoveRange.Compute(Enumerable.Repeat(50m, 20).ToList(), 50)!;

        Assert.Equal(0m, range.TypicalMovePercent);
        Assert.Equal(50m, range.Low95);
        Assert.Equal(50m, range.High95);
        Assert.Equal(0m, range.UpDaysPercent);
    }

    [Fact]
    public void Too_few_days_returns_null()
    {
        Assert.Null(DailyMoveRange.Compute(Alternating(DailyMoveRange.MinDays - 1), 100));
        Assert.NotNull(DailyMoveRange.Compute(Alternating(DailyMoveRange.MinDays), 100));
    }

    [Fact]
    public void Longer_horizon_scales_sigma_by_square_root_of_days()
    {
        List<decimal> closes = Alternating(60);
        DailyMoveRange oneDay = DailyMoveRange.Compute(closes, 100)!;
        DailyMoveRange thirtyDays = DailyMoveRange.Compute(closes, 100, 30)!;

        // ln(cao/giá) = σ: 30 ngày rộng hơn 1 ngày đúng √30 lần.
        double ratio = Math.Log((double)(thirtyDays.High68 / 100)) / Math.Log((double)(oneDay.High68 / 100));
        Assert.Equal(Math.Sqrt(30), ratio, 6);
        Assert.Equal(30, thirtyDays.HorizonDays);
    }

    [Fact]
    public void History_stats_use_horizon_length_windows()
    {
        // Tăng đều 1%/ngày: mọi đoạn 30 ngày đều tăng ~34,8%.
        List<decimal> closes = [100m];
        for (int index = 0; index < 40; index++)
            closes.Add(closes[^1] * 1.01m);

        DailyMoveRange range = DailyMoveRange.Compute(closes, 100, 30)!;

        Assert.Equal(11, range.Samples);
        Assert.Equal(100m, range.UpDaysPercent);
        Assert.InRange(range.LargestRisePercent, 34.7m, 34.9m);
        Assert.InRange(range.LargestDropPercent, 34.7m, 34.9m);
    }

    [Fact]
    public void Too_little_history_for_horizon_returns_null()
    {
        Assert.Null(DailyMoveRange.Compute(Alternating(30), 100, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyMoveRange.Compute(Alternating(30), 100, 0));
    }

    [Fact]
    public void Rejects_non_positive_prices()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyMoveRange.Compute(Alternating(20), 0));
        Assert.Throws<ArgumentException>(() => DailyMoveRange.Compute([1m, 0m, 1m], 1));
    }
}
