using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

public class DailyMoveReportTests
{
    private static readonly DateTime T0 = TestCandles.T0;

    private static Candle C(int hour, decimal open, decimal high, decimal low, decimal close, decimal volume = 1,
        decimal? takerBuy = null, bool closed = true) =>
        new("Binance", "BTCUSDT", CandleInterval.H1, T0.AddHours(hour), open, high, low, close, volume, closed, T0,
            takerBuyVolume: takerBuy);

    // Lên đỉnh 106 ở giờ 1, rơi về đáy 94 ở giờ 2, hồi lên 103 ở giờ 3.
    private static readonly List<Candle> Swing =
    [
        C(0, 100, 105, 99, 104),
        C(1, 104, 106, 95, 96),
        C(2, 96, 98, 94, 97),
        C(3, 97, 103, 96, 102),
    ];

    [Fact]
    public void Summarizes_open_close_high_low_and_range()
    {
        var report = DailyMoveReport.Build(Swing, T0, T0.AddHours(4))!;

        Assert.Equal(4, report.CandleCount);
        Assert.Equal(100m, report.Open);
        Assert.Equal(102m, report.Close);
        Assert.Equal(2m, report.ChangePercent);
        Assert.Equal(106m, report.High);
        Assert.Equal(T0.AddHours(1), report.HighTimeUtc);
        Assert.Equal(94m, report.Low);
        Assert.Equal(T0.AddHours(2), report.LowTimeUtc);
        Assert.Equal(12m, report.RangePercent);
    }

    [Fact]
    public void Drawdown_is_measured_from_the_peak_before_the_low_and_rally_from_the_low_before_the_high()
    {
        var report = DailyMoveReport.Build(Swing, T0, T0.AddHours(4))!;

        // Đỉnh 106 (giờ 1) → đáy 94 (giờ 2); đáy 94 (giờ 2) → 103 (giờ 3).
        Assert.Equal((94m / 106m - 1) * 100, report.MaxDrawdownPercent);
        Assert.Equal((103m / 94m - 1) * 100, report.MaxRallyPercent);
    }

    [Fact]
    public void Low_inside_the_same_candle_as_the_high_is_not_counted_as_a_drop_from_that_high()
    {
        // Không biết đỉnh 110 hay đáy 90 đến trước, nên chỉ so đáy với giá mở 100.
        var report = DailyMoveReport.Build([C(0, 100, 110, 90, 100)], T0, T0.AddHours(1))!;

        Assert.Equal(-10m, report.MaxDrawdownPercent);
        Assert.Equal(10m, report.MaxRallyPercent);
    }

    [Fact]
    public void Finds_the_biggest_rising_and_falling_candles()
    {
        var report = DailyMoveReport.Build(Swing, T0, T0.AddHours(4))!;

        Assert.Equal(T0.AddHours(3), report.BiggestRise.OpenTimeUtc);
        Assert.Equal(T0.AddHours(1), report.BiggestDrop.OpenTimeUtc);
        Assert.Equal((96m / 104m - 1) * 100, report.BiggestDrop.ChangePercent);
    }

    [Fact]
    public void Only_candles_fully_inside_the_period_are_reported()
    {
        var report = DailyMoveReport.Build(Swing, T0.AddHours(1), T0.AddHours(3).AddMinutes(30))!;

        Assert.Equal(2, report.CandleCount);
        Assert.Equal(104m, report.Open);
        Assert.Equal(97m, report.Close);
    }

    [Fact]
    public void Volume_spike_compares_with_the_average_of_the_previous_candles()
    {
        List<Candle> candles =
        [
            C(0, 100, 100, 100, 100, volume: 10),
            C(1, 100, 100, 100, 100, volume: 10),
            C(2, 100, 100, 100, 100, volume: 10),
            C(3, 100, 100, 90, 92, volume: 40),
            C(4, 92, 93, 92, 93, volume: 10),
        ];

        var report = DailyMoveReport.Build(candles, T0.AddHours(3), T0.AddHours(5), baselineCandles: 3)!;

        var spike = Assert.Single(report.VolumeSpikes);
        Assert.Equal(T0.AddHours(3), spike.OpenTimeUtc);
        Assert.Equal(4m, spike.VolumeRatio);
        Assert.Equal(50m, report.Volume);
    }

    [Fact]
    public void Candle_without_enough_earlier_candles_has_no_volume_ratio()
    {
        var report = DailyMoveReport.Build(Swing, T0, T0.AddHours(4), baselineCandles: 2)!;

        // Nến giờ 1 chỉ có 1 nến trước nó; nến giờ 3 có đủ 2 nến (khối lượng đều 1).
        Assert.Null(report.BiggestDrop.VolumeRatio);
        Assert.Equal(1m, report.BiggestRise.VolumeRatio);
        Assert.Empty(report.VolumeSpikes);
    }

    [Fact]
    public void Buy_share_is_null_when_any_candle_lacks_taker_buy_volume()
    {
        List<Candle> all = [C(0, 1, 1, 1, 1, volume: 10, takerBuy: 6), C(1, 1, 1, 1, 1, volume: 10, takerBuy: 2)];
        List<Candle> missing = [C(0, 1, 1, 1, 1, volume: 10, takerBuy: 6), C(1, 1, 1, 1, 1, volume: 10)];

        Assert.Equal(40m, DailyMoveReport.Build(all, T0, T0.AddHours(2))!.BuyPercent);
        Assert.Null(DailyMoveReport.Build(missing, T0, T0.AddHours(2))!.BuyPercent);
    }

    [Fact]
    public void Period_without_candles_has_no_report()
    {
        Assert.Null(DailyMoveReport.Build(Swing, T0.AddDays(1), T0.AddDays(2)));
    }

    [Fact]
    public void Rejects_open_or_unordered_candles_and_empty_period()
    {
        Assert.Throws<ArgumentException>(() =>
            DailyMoveReport.Build([C(0, 1, 1, 1, 1, closed: false)], T0, T0.AddHours(1)));
        Assert.Throws<ArgumentException>(() =>
            DailyMoveReport.Build([C(1, 1, 1, 1, 1), C(0, 1, 1, 1, 1)], T0, T0.AddHours(2)));
        Assert.Throws<ArgumentException>(() => DailyMoveReport.Build(Swing, T0, T0));
    }
}
