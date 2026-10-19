using CryptoBot.Domain.Indicators;

namespace CryptoBot.Domain.Tests;

public class RsiTests
{
    [Fact]
    public void Seeds_with_simple_average_then_uses_wilder_smoothing()
    {
        // Thay đổi: +2, -1, +3, -2
        var rsi = Rsi.Calculate([10m, 12m, 11m, 14m, 12m], 2);

        Assert.Null(rsi[0]);
        Assert.Null(rsi[1]);
        // Seed: lãi TB 1, lỗ TB 0.5 → RS 2 → 66.67
        Assert.Equal(66.6667m, Math.Round(rsi[2]!.Value, 4));
        // lãi (1+3)/2 = 2, lỗ (0.5+0)/2 = 0.25 → RS 8 → 88.89
        Assert.Equal(88.8889m, Math.Round(rsi[3]!.Value, 4));
        // lãi (2+0)/2 = 1, lỗ (0.25+2)/2 = 1.125 → 47.06
        Assert.Equal(47.0588m, Math.Round(rsi[4]!.Value, 4));
    }

    [Fact]
    public void Only_gains_is_100_and_flat_is_50()
    {
        Assert.Equal(100m, Rsi.Calculate([1m, 2m, 3m], 2)[2]);
        Assert.Equal(50m, Rsi.Calculate([5m, 5m, 5m], 2)[2]);
    }

    [Fact]
    public void Not_enough_values_returns_all_null() =>
        Assert.All(Rsi.Calculate([1m, 2m], 2), v => Assert.Null(v));
}
