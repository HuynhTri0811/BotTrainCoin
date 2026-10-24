using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

public class DailyMoveRangeTests
{
    /// <summary>Giá xen kẽ +2% / −2% mỗi ngày.</summary>
    private static List<decimal> Alternating(int days)
    {
        List<decimal> closes = [100m];
        for (int i = 0; i < days; i++)
            closes.Add(closes[^1] * (i % 2 == 0 ? 1.02m : 0.98m));
        return closes;
    }

    [Fact]
    public void Range_is_symmetric_around_price_and_95_wider_than_68()
    {
        DailyMoveRange r = DailyMoveRange.Compute(Alternating(30), 1000)!;

        Assert.Equal(30, r.Days);
        Assert.True(r.Low95 < r.Low68 && r.Low68 < 1000 && 1000 < r.High68 && r.High68 < r.High95);
        // Nhân/chia cùng một hệ số: thấp × cao = giá².
        Assert.Equal(1000m * 1000m, Math.Round(r.Low68 * r.High68, 6));
    }

    [Fact]
    public void Typical_move_matches_daily_swing()
    {
        DailyMoveRange r = DailyMoveRange.Compute(Alternating(30), 100)!;

        Assert.InRange(r.TypicalMovePercent, 1.9m, 2.1m);
        Assert.InRange(r.AverageMovePercent, 1.99m, 2.01m);
        Assert.Equal(2m, Math.Round(r.LargestRisePercent, 6));
        Assert.Equal(-2m, Math.Round(r.LargestDropPercent, 6));
        Assert.Equal(50m, r.UpDaysPercent);
    }

    [Fact]
    public void Flat_prices_have_zero_range()
    {
        DailyMoveRange r = DailyMoveRange.Compute(Enumerable.Repeat(50m, 20).ToList(), 50)!;

        Assert.Equal(0m, r.TypicalMovePercent);
        Assert.Equal(50m, r.Low95);
        Assert.Equal(50m, r.High95);
        Assert.Equal(0m, r.UpDaysPercent);
    }

    [Fact]
    public void Too_few_days_returns_null()
    {
        Assert.Null(DailyMoveRange.Compute(Alternating(DailyMoveRange.MinDays - 1), 100));
        Assert.NotNull(DailyMoveRange.Compute(Alternating(DailyMoveRange.MinDays), 100));
    }

    [Fact]
    public void Rejects_non_positive_prices()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyMoveRange.Compute(Alternating(20), 0));
        Assert.Throws<ArgumentException>(() => DailyMoveRange.Compute([1m, 0m, 1m], 1));
    }
}
