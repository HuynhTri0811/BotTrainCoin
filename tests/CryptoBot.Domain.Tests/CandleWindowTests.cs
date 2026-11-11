using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

public class CandleWindowTests
{
    [Fact]
    public void Continuous_window_has_no_gap()
    {
        List<Candle> window = [TestCandles.At(0, 1), TestCandles.At(1, 1), TestCandles.At(2, 1)];

        Assert.False(CandleWindow.HasGap(window, CandleInterval.H1));
    }

    [Fact]
    public void Missing_candle_is_a_gap()
    {
        List<Candle> window = [TestCandles.At(0, 1), TestCandles.At(1, 1), TestCandles.At(3, 1)];

        Assert.True(CandleWindow.HasGap(window, CandleInterval.H1));
    }

    [Fact]
    public void Single_candle_has_no_gap()
    {
        Assert.False(CandleWindow.HasGap([TestCandles.At(5, 1)], CandleInterval.H1));
    }
}
