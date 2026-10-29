using CryptoBot.Domain.Common;
using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Tests;

/// <summary>Các hàm nhỏ tách ra từ chiến lược EMA và chỉ báo MyRsi.</summary>
public class SignalBuildingBlockTests
{
    private static decimal[] Walk(int n, int seed)
    {
        var rnd = new Random(seed);
        var v = new decimal[n];
        var p = 100m;
        for (var i = 0; i < n; i++)
            v[i] = p = Math.Round(p * (1 + (decimal)((rnd.NextDouble() - 0.5) * 0.04)), 2);
        return v;
    }

    [Theory]
    [InlineData(3, 4)]
    [InlineData(9, 300)]
    [InlineData(21, 22)]
    public void Ema_last_two_equals_tail_of_full_series(int period, int length)
    {
        var values = Walk(length, period);
        var full = Ema.Calculate(values, period);

        var (prev, last) = Ema.LastTwo(values, period);

        Assert.Equal(full[^2], prev);
        Assert.Equal(full[^1], last);
    }

    [Theory]
    [InlineData(2, 4)]
    [InlineData(14, 200)]
    [InlineData(7, 9)]
    public void Rsi_last_two_equals_tail_of_full_series(int period, int length)
    {
        var values = Walk(length, period);
        var full = Rsi.Calculate(values, period);

        var (prev, last) = Rsi.LastTwo(values, period);

        Assert.Equal(full[^2], prev);
        Assert.Equal(full[^1], last);
    }

    [Fact]
    public void Last_two_rejects_too_short_input()
    {
        Assert.Throws<ArgumentException>(() => Ema.LastTwo([1m, 2m, 3m], 3));
        Assert.Throws<ArgumentException>(() => Rsi.LastTwo([1m, 2m, 3m, 4m], 3));
    }

    [Theory]
    [InlineData(10, 10, 12, 11, true, false)]   // bằng nhau rồi vượt lên
    [InlineData(9, 10, 12, 11, true, false)]    // dưới rồi vượt lên
    [InlineData(10, 10, 9, 11, false, true)]    // bằng nhau rồi rơi xuống
    [InlineData(11, 10, 12, 11, false, false)]  // đã ở trên từ trước
    [InlineData(10, 10, 10, 10, false, false)]  // đứng yên
    public void Crossover_detects_only_fresh_crosses(int aPrev, int bPrev, int a, int b, bool up, bool down)
    {
        Assert.Equal(up, Crossover.Up(aPrev, bPrev, a, b));
        Assert.Equal(down, Crossover.Down(aPrev, bPrev, a, b));
    }

    [Fact]
    public void Crossover_through_level_matches_rsi_rules()
    {
        Assert.True(Crossover.UpThrough(29.9m, 30.1m, 30m));
        Assert.True(Crossover.UpThrough(30m, 30.1m, 30m));
        Assert.False(Crossover.UpThrough(30.1m, 31m, 30m));
        Assert.True(Crossover.DownThrough(70.2m, 69.9m, 70m));
        Assert.False(Crossover.DownThrough(69m, 68m, 70m));
    }

    [Fact]
    public void List_slice_is_a_view_without_copying()
    {
        int[] source = [0, 1, 2, 3, 4, 5];

        var slice = new ListSlice<int>(source, 2, 3);

        Assert.Equal(3, slice.Count);
        Assert.Equal([2, 3, 4], slice);
        Assert.Equal(4, slice[^1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => slice[3]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ListSlice<int>(source, 4, 3));
    }

    [Fact]
    public void Ema_strategy_span_overload_matches_candle_overload()
    {
        var strategy = new EmaCrossStrategy(new EmaCrossParameters(2, 3, 4));
        var closes = new decimal[] { 7, 10, 10, 10, 13 };
        var candles = TestCandles.FromCloses(closes);

        Assert.Equal(strategy.ComputeAt(candles), strategy.ComputeAt(closes));
        Assert.Null(strategy.ComputeAt(closes.AsSpan(0, 3)));
    }
}
