using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

public class FlowRsiIndicatorTests
{
    private static FlowRsiIndicator Flow2() => new() { Period = 2, LookbackCandles = 4 };

    /// <summary>Nến có chênh lệch mua − bán = <paramref name="netFlow"/> trên tổng khối lượng <paramref name="volume"/>.</summary>
    private static Candle FlowCandle(int index, decimal netFlow, decimal volume = 10m, decimal close = 100m) =>
        new("Binance", "BTCUSDT", CandleInterval.H1, TestCandles.T0 + TimeSpan.FromHours(index), close, close, close,
            close, volume, true, TestCandles.T0, takerBuyVolume: (volume + netFlow) / 2);

    private static List<Candle> FromFlows(params decimal[] flows) =>
        flows.Select((flow, index) => FlowCandle(index, flow)).ToList();

    [Fact]
    public void Buyers_taking_over_after_selling_is_long()
    {
        // Bán áp đảo 3 nến (Flow RSI 0) rồi mua áp đảo mạnh: lãi TB 5, lỗ TB 0.5 → 100 − 100/11 = 90.91.
        var result = Flow2().Compute(FromFlows(-1m, -1m, -1m, 10m));

        Assert.Equal(IndicatorSignal.Long, result.Signal);
        Assert.Equal(90.91m, result.Values["flowRsi"]);
        Assert.Contains("mua chủ động 100%", result.Reason);
    }

    [Fact]
    public void Sellers_taking_over_after_buying_is_short()
    {
        var result = Flow2().Compute(FromFlows(1m, 1m, 1m, -10m));

        Assert.Equal(IndicatorSignal.Short, result.Signal);
        Assert.Equal(9.09m, result.Values["flowRsi"]);
    }

    [Fact]
    public void Uses_taker_flow_not_price()
    {
        // Giá giảm đều (RSI giá = 0) nhưng bên mua áp đảo ở nến cuối: vẫn Long.
        var candles = new[] { -1m, -1m, -1m, 10m }
            .Select((flow, index) => FlowCandle(index, flow, close: 100m - index)).ToList();

        var result = Flow2().Compute(candles);

        Assert.Equal(IndicatorSignal.Long, result.Signal);
    }

    [Fact]
    public void Selling_pressure_that_continues_is_no_signal()
    {
        var result = Flow2().Compute(FromFlows(-1m, -1m, -1m, -1m));

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Equal(0m, result.Values["flowRsi"]);
    }

    [Fact]
    public void Missing_taker_volume_inside_the_window_is_not_computed()
    {
        var candles = FromFlows(-1m, -1m, -1m, 10m);
        candles[1] = TestCandles.At(1, 100m);

        var result = Flow2().Compute(candles);

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Contains("Thiếu khối lượng mua chủ động ở 1/4 nến", result.Reason);
        Assert.Empty(result.Values);
    }

    [Fact]
    public void Uses_only_the_last_lookback_candles()
    {
        // Nến cũ phía trước, kể cả nến thiếu số liệu mua chủ động, không đổi kết quả: cửa sổ cố định.
        var candles = new List<Candle> { TestCandles.At(-2, 100m), FlowCandle(-1, 10m) };
        candles.AddRange(FromFlows(-1m, -1m, -1m, 10m));

        var result = Flow2().Compute(candles);

        Assert.Equal(IndicatorSignal.Long, result.Signal);
        Assert.Equal(90.91m, result.Values["flowRsi"]);
    }

    [Fact]
    public void Warm_up_without_enough_candles()
    {
        var result = Flow2().Compute(FromFlows(1m, 1m));

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Contains("Warm-up", result.Reason);
    }

    [Theory]
    [InlineData(1, 30, 70, 200)]
    [InlineData(14, 70, 30, 200)]
    [InlineData(14, 0, 70, 200)]
    [InlineData(14, 30, 100, 200)]
    [InlineData(14, 30, 70, 15)]
    public void Invalid_parameters_are_rejected(int period, int oversold, int overbought, int lookback)
    {
        var indicator = new FlowRsiIndicator
        {
            Period = period, Oversold = oversold, Overbought = overbought, LookbackCandles = lookback,
        };

        Assert.NotNull(indicator.Validate());
    }

    [Fact]
    public void Defaults_are_valid_and_draw_one_line_with_levels()
    {
        var indicator = new FlowRsiIndicator();

        Assert.Null(indicator.Validate());
        var line = Assert.Single(indicator.Lines);
        Assert.Equal("flowRsi", line.Key);
        Assert.False(line.Overlay);
        Assert.Equal([30m, 70m], line.Levels);
    }
}
