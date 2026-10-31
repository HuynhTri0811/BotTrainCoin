using CryptoBot.Domain.Indicators;

namespace CryptoBot.Domain.Tests;

public class MyRsiIndicatorTests
{
    private static MyRsiIndicator Rsi2(int trendEma = 0) =>
        new() { Period = 2, LookbackCandles = 5, TrendEmaPeriod = trendEma };

    [Fact]
    public void Crossing_up_through_oversold_is_long()
    {
        var result = Rsi2().Compute(TestCandles.FromCloses(100m, 99m, 98m, 97m, 110m));

        Assert.Equal(IndicatorSignal.Long, result.Signal);
        Assert.Equal(92.86m, result.Values["rsi"]);
    }

    [Fact]
    public void Crossing_down_through_overbought_is_short()
    {
        var result = Rsi2().Compute(TestCandles.FromCloses(100m, 101m, 102m, 103m, 90m));

        Assert.Equal(IndicatorSignal.Short, result.Signal);
    }

    [Fact]
    public void Staying_oversold_is_no_signal()
    {
        var result = Rsi2().Compute(TestCandles.FromCloses(100m, 99m, 98m, 97m, 96m));

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Equal(0m, result.Values["rsi"]);
    }

    [Fact]
    public void Uses_only_the_last_lookback_candles()
    {
        // Thêm nến cũ phía trước không đổi kết quả: cửa sổ cố định.
        var result = Rsi2().Compute(TestCandles.FromCloses(500m, 1m, 100m, 99m, 98m, 97m, 110m));

        Assert.Equal(92.86m, result.Values["rsi"]);
        Assert.Equal(IndicatorSignal.Long, result.Signal);
    }

    [Fact]
    public void Warm_up_without_enough_candles()
    {
        var result = Rsi2().Compute(TestCandles.FromCloses(1m, 2m));

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Contains("Warm-up", result.Reason);
    }

    [Fact]
    public void Trend_filter_blocks_long_below_ema()
    {
        var ind = new MyRsiIndicator { Period = 2, TrendEmaPeriod = 3, LookbackCandles = 6 };
        // Giá hồi lên nhưng vẫn dưới EMA3 của chuỗi giảm mạnh.
        var result = ind.Compute(TestCandles.FromCloses(200m, 150m, 120m, 100m, 90m, 105m));

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Contains("dưới EMA3", result.Reason);
        Assert.Equal(2, ind.Lines.Count);
    }

    private static MyRsiIndicator Pullback() => new() { Period = 2, TrendEmaPeriod = 3, LookbackCandles = 6 };

    [Fact]
    public void Trend_filter_longs_a_dip_in_an_uptrend()
    {
        // RSI2: 33.3 → 75 (cắt lên 40); EMA3 = 125 < giá 135.
        var result = Pullback().Compute(TestCandles.FromCloses(100m, 110m, 120m, 130m, 110m, 135m));

        Assert.Equal(IndicatorSignal.Long, result.Signal);
        Assert.Contains("trên EMA3", result.Reason);
    }

    [Fact]
    public void Trend_filter_shorts_a_bounce_in_a_downtrend()
    {
        // RSI2: 66.7 → 25 (cắt xuống 60); EMA3 = 175 > giá 165.
        var result = Pullback().Compute(TestCandles.FromCloses(200m, 190m, 180m, 170m, 190m, 165m));

        Assert.Equal(IndicatorSignal.Short, result.Signal);
        Assert.Contains("dưới EMA3", result.Reason);
    }

    [Fact]
    public void Trend_filter_blocks_short_above_ema()
    {
        // RSI2: 100 → 55.6 (cắt xuống 60) nhưng giá 150 vẫn trên EMA3 = 147.5.
        var result = Pullback().Compute(TestCandles.FromCloses(100m, 110m, 120m, 130m, 170m, 150m));

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Contains("trên EMA3", result.Reason);
    }

    [Fact]
    public void Trend_filter_draws_pullback_levels()
    {
        var line = Pullback().Lines.Single(candidate => candidate.Key == "rsi");

        Assert.Equal([40m, 60m], line.Levels);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(40, 100)]
    public void Pullback_levels_must_be_inside_0_100(decimal longLevel, decimal shortLevel)
    {
        var ind = new MyRsiIndicator { TrendEmaPeriod = 50, PullbackLong = longLevel, PullbackShort = shortLevel };

        Assert.Contains("Pullback", ind.Validate());
    }
}
