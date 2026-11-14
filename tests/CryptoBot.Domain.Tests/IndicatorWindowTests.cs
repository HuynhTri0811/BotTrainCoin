using CryptoBot.Domain.Indicators;

namespace CryptoBot.Domain.Tests;

public class IndicatorWindowTests
{
    [Fact]
    public void Last_closes_returns_the_tail_in_time_order()
    {
        var candles = TestCandles.FromCloses(1, 2, 3, 4, 5);

        Assert.Equal([3m, 4m, 5m], IndicatorWindow.LastCloses(candles, 3));
        Assert.Equal([1m, 2m, 3m, 4m, 5m], IndicatorWindow.LastCloses(candles, 5));
        Assert.Empty(IndicatorWindow.LastCloses(candles, 0));
    }

    [Fact]
    public void Copy_last_closes_fills_the_whole_destination_with_the_tail()
    {
        var candles = TestCandles.FromCloses(1, 2, 3, 4, 5);
        var destination = new decimal[3];

        IndicatorWindow.CopyLastCloses(candles, destination);

        Assert.Equal([3m, 4m, 5m], destination);
        Assert.Throws<ArgumentException>(() => IndicatorWindow.CopyLastCloses(candles, new decimal[6]));
    }

    [Fact]
    public void Last_closes_rejects_asking_for_more_than_available()
    {
        var ex = Assert.Throws<ArgumentException>(() => IndicatorWindow.LastCloses(TestCandles.FromCloses(1, 2), 3));

        Assert.Contains("Cần 3 nến, chỉ có 2", ex.Message);
    }

    [Fact]
    public void Template_indicator_signals_long_when_close_breaks_previous_high()
    {
        var template = new TemplateIndicator { Period = 3 };

        var breakout = template.Compute(TestCandles.FromCloses(10, 12, 11, 13));
        var inside = template.Compute(TestCandles.FromCloses(10, 12, 11, 12));

        Assert.Equal(IndicatorSignal.Long, breakout.Signal);
        Assert.Equal(12m, breakout.Values["high"]);
        Assert.Equal(IndicatorSignal.None, inside.Signal);
    }

    [Fact]
    public void Template_indicator_warms_up_and_validates_period()
    {
        var template = new TemplateIndicator { Period = 3 };

        Assert.Equal(4, template.RequiredCandles);
        Assert.Equal(IndicatorSignal.None, template.Compute(TestCandles.FromCloses(10, 12, 13)).Signal);
        Assert.Null(template.Validate());
        Assert.NotNull(new TemplateIndicator { Period = 0 }.Validate());
    }
}
