using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Tests;

public class EmaTests
{
    [Fact]
    public void Seeds_with_sma_then_applies_alpha()
    {
        // period 3: seed = (2+4+6)/3 = 4; alpha = 0.5
        // t3 = 0.5*8 + 0.5*4 = 6; t4 = 0.5*10 + 0.5*6 = 8
        var ema = Ema.Calculate([2m, 4m, 6m, 8m, 10m], 3);

        Assert.Equal([null, null, 4m, 6m, 8m], ema);
    }

    [Fact]
    public void Hand_computed_period_9_fixture()
    {
        // 9 nến đầu = 1..9 -> SMA = 5; alpha = 0.2; close tiếp theo 15 -> 0.2*15 + 0.8*5 = 7
        decimal[] closes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 15];

        var ema = Ema.Calculate(closes, 9);

        Assert.Equal(5m, ema[8]);
        Assert.Equal(7m, ema[9]);
    }

    [Fact]
    public void Returns_all_null_when_history_shorter_than_period()
    {
        var ema = Ema.Calculate([1m, 2m], 3);

        Assert.All(ema, value => Assert.Null(value));
    }

    [Fact]
    public void Same_input_gives_same_output()
    {
        decimal[] closes = [100.12m, 101.5m, 99.87m, 102.33m, 103m, 101.01m];

        Assert.Equal(Ema.Calculate(closes, 3), Ema.Calculate(closes, 3));
    }
}
