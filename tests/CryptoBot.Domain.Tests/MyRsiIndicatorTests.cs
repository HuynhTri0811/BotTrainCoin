using CryptoBot.Domain.Indicators;

namespace CryptoBot.Domain.Tests;

public class MyRsiIndicatorTests
{
    private static MyRsiIndicator Rsi2(int trendEma = 0) =>
        new() { Period = 2, LookbackCandles = 5, TrendEmaPeriod = trendEma };

    [Fact]
    public void Crossing_up_through_oversold_is_long()
    {
        var r = Rsi2().Compute(TestCandles.FromCloses(100m, 99m, 98m, 97m, 110m));

        Assert.Equal(IndicatorSignal.Long, r.Signal);
        Assert.Equal(92.86m, r.Values["rsi"]);
    }

    [Fact]
    public void Crossing_down_through_overbought_is_short()
    {
        var r = Rsi2().Compute(TestCandles.FromCloses(100m, 101m, 102m, 103m, 90m));

        Assert.Equal(IndicatorSignal.Short, r.Signal);
    }

    [Fact]
    public void Staying_oversold_is_no_signal()
    {
        var r = Rsi2().Compute(TestCandles.FromCloses(100m, 99m, 98m, 97m, 96m));

        Assert.Equal(IndicatorSignal.None, r.Signal);
        Assert.Equal(0m, r.Values["rsi"]);
    }

    [Fact]
    public void Uses_only_the_last_lookback_candles()
    {
        // Thêm nến cũ phía trước không đổi kết quả: cửa sổ cố định.
        var r = Rsi2().Compute(TestCandles.FromCloses(500m, 1m, 100m, 99m, 98m, 97m, 110m));

        Assert.Equal(92.86m, r.Values["rsi"]);
        Assert.Equal(IndicatorSignal.Long, r.Signal);
    }

    [Fact]
    public void Warm_up_without_enough_candles()
    {
        var r = Rsi2().Compute(TestCandles.FromCloses(1m, 2m));

        Assert.Equal(IndicatorSignal.None, r.Signal);
        Assert.Contains("Warm-up", r.Reason);
    }

    [Fact]
    public void Trend_filter_blocks_long_below_ema()
    {
        var ind = new MyRsiIndicator { Period = 2, TrendEmaPeriod = 3, LookbackCandles = 6 };
        // Giá hồi lên nhưng vẫn dưới EMA3 của chuỗi giảm mạnh.
        var r = ind.Compute(TestCandles.FromCloses(200m, 150m, 120m, 100m, 90m, 105m));

        Assert.Equal(IndicatorSignal.None, r.Signal);
        Assert.Contains("dưới EMA3", r.Reason);
        Assert.Equal(2, ind.Lines.Count);
    }
}
