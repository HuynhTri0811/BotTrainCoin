using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Tests;

public class MacdIndicatorTests
{
    // EMA1 = giá đóng cửa, EMA2 và đường tín hiệu EMA2: đủ nhỏ để tính tay.
    private static MacdIndicator Small() =>
        new() { FastPeriod = 1, SlowPeriod = 2, SignalPeriod = 2, LookbackCandles = 4 };

    [Fact]
    public void Macd_crossing_above_its_signal_line_is_long()
    {
        // EMA2: 95 → 78.33 → 92.78. MACD: -5 → -8.33 → 7.22. Tín hiệu: -6.67 → 2.59.
        IndicatorResult result = Small().Compute(TestCandles.FromCloses(100m, 90m, 70m, 100m));

        Assert.Equal(IndicatorSignal.Long, result.Signal);
        Assert.Equal(7.2222m, result.Values["macd"]);
        Assert.Equal(2.5926m, result.Values["signal"]);
        Assert.Contains("cắt lên", result.Reason);
    }

    [Fact]
    public void Macd_crossing_below_its_signal_line_is_short()
    {
        IndicatorResult result = Small().Compute(TestCandles.FromCloses(100m, 110m, 130m, 100m));

        Assert.Equal(IndicatorSignal.Short, result.Signal);
        Assert.Equal(-7.2222m, result.Values["macd"]);
        Assert.Equal(-2.5926m, result.Values["signal"]);
    }

    [Fact]
    public void Macd_staying_below_its_signal_line_is_no_signal()
    {
        IndicatorResult result = Small().Compute(TestCandles.FromCloses(100m, 90m, 70m, 40m));

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Equal(-12.7778m, result.Values["macd"]);
    }

    [Fact]
    public void Uses_only_the_last_lookback_candles()
    {
        // Thêm nến cũ phía trước không đổi kết quả: cửa sổ cố định.
        IndicatorResult result = Small().Compute(TestCandles.FromCloses(5m, 900m, 100m, 90m, 70m, 100m));

        Assert.Equal(IndicatorSignal.Long, result.Signal);
        Assert.Equal(7.2222m, result.Values["macd"]);
    }

    [Fact]
    public void Warm_up_without_enough_candles()
    {
        IndicatorResult result = Small().Compute(TestCandles.FromCloses(1m, 2m, 3m));

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Contains("Warm-up", result.Reason);
        Assert.Empty(result.Values);
    }

    [Fact]
    public void A_fall_then_a_rise_gives_one_long_and_no_short_after_it()
    {
        MacdIndicator indicator = new() { FastPeriod = 3, SlowPeriod = 6, SignalPeriod = 3, LookbackCandles = 20 };
        // 30 nến giảm đều rồi 30 nến tăng đều.
        decimal[] closes = Enumerable.Range(0, 60)
            .Select(index => index < 30 ? 200m - index * 2 : 142m + (index - 30) * 2)
            .ToArray();
        List<Candle> candles = TestCandles.FromCloses(closes);

        List<IndicatorSignal> signals = Enumerable.Range(indicator.LookbackCandles, 60 - indicator.LookbackCandles + 1)
            .Select(count => indicator.Compute(candles.GetRange(0, count)).Signal)
            .Where(signal => signal != IndicatorSignal.None)
            .ToList();

        Assert.Equal([IndicatorSignal.Long], signals);
    }

    [Fact]
    public void Macd_value_is_fast_ema_minus_slow_ema()
    {
        MacdIndicator indicator = new() { FastPeriod = 3, SlowPeriod = 6, SignalPeriod = 3, LookbackCandles = 12 };
        decimal[] closes = [10m, 12m, 11m, 15m, 14m, 18m, 17m, 16m, 20m, 19m, 23m, 21m];

        IndicatorResult result = indicator.Compute(TestCandles.FromCloses(closes));

        decimal expected = Ema.Calculate(closes, 3)[^1]!.Value - Ema.Calculate(closes, 6)[^1]!.Value;
        Assert.Equal(Math.Round(expected, 4), result.Values["macd"]);
    }

    [Fact]
    public void Macd_matches_ema_calculate_over_a_long_window()
    {
        MacdIndicator indicator = new() { FastPeriod = 5, SlowPeriod = 13, SignalPeriod = 4, LookbackCandles = 60 };
        decimal[] closes = Enumerable.Range(0, 60).Select(index => 100m + (index * 37 % 23) - index * 0.5m).ToArray();

        IndicatorResult result = indicator.Compute(TestCandles.FromCloses(closes));

        decimal[] expectedLine = Enumerable.Range(12, 48)
            .Select(index => Ema.Calculate(closes, 5)[index]!.Value - Ema.Calculate(closes, 13)[index]!.Value)
            .ToArray();
        Assert.Equal(Math.Round(expectedLine[^1], 4), result.Values["macd"]);
        Assert.Equal(Math.Round(Ema.Calculate(expectedLine, 4)[^1]!.Value, 4), result.Values["signal"]);
    }

    [Fact]
    public void Lines_match_the_value_keys()
    {
        MacdIndicator indicator = Small();

        IndicatorResult result = indicator.Compute(TestCandles.FromCloses(100m, 90m, 70m, 100m));

        Assert.Equal(indicator.Lines.Select(line => line.Key).Order(), result.Values.Keys.Order());
    }

    [Fact]
    public void Default_parameters_are_the_slow_macd_and_valid()
    {
        MacdIndicator indicator = new();

        Assert.Null(indicator.Validate());
        Assert.Equal((96, 208, 72, 600),
            (indicator.FastPeriod, indicator.SlowPeriod, indicator.SignalPeriod, indicator.RequiredCandles));
    }

    [Theory]
    [InlineData(0, 2, 2, 10)]   // FastPeriod < 1
    [InlineData(3, 3, 2, 10)]   // SlowPeriod không lớn hơn FastPeriod
    [InlineData(1, 2, 0, 10)]   // SignalPeriod < 1
    [InlineData(1, 2, 1, 10)]   // SignalPeriod = 1: đường tín hiệu trùng đường MACD
    [InlineData(1, 6, 5, 10)]   // LookbackCandles < SlowPeriod + SignalPeriod
    public void Validate_rejects_bad_parameters(int fast, int slow, int signal, int lookback)
    {
        MacdIndicator indicator = new()
        {
            FastPeriod = fast, SlowPeriod = slow, SignalPeriod = signal, LookbackCandles = lookback,
        };

        Assert.NotNull(indicator.Validate());
    }

    [Fact]
    public void Smallest_valid_lookback_still_computes()
    {
        // LookbackCandles = SlowPeriod + SignalPeriod: vừa đủ 2 điểm của đường tín hiệu.
        MacdIndicator indicator = new() { FastPeriod = 2, SlowPeriod = 4, SignalPeriod = 3, LookbackCandles = 7 };

        Assert.Null(indicator.Validate());
        IndicatorResult result = indicator.Compute(TestCandles.FromCloses(10m, 11m, 12m, 13m, 14m, 15m, 16m));
        Assert.NotNull(result.Values["signal"]);
    }
}
