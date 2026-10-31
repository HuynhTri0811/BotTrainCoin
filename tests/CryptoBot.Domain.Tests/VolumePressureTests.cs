using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

public class VolumePressureTests
{
    [Fact]
    public void Sell_80_percent_larger_than_buy_is_sell_heavy()
    {
        // Mua 10, bán 18: bán lớn hơn mua đúng 80%.
        VolumePressure pressure = VolumePressure.Analyze(28, 10, 80);

        Assert.Equal(VolumePressureSide.SellHeavy, pressure.Side);
        Assert.True(pressure.IsAlert);
        Assert.Equal(10m, pressure.BuyVolume);
        Assert.Equal(18m, pressure.SellVolume);
        Assert.Equal(80m, pressure.ExcessPercent);
    }

    [Fact]
    public void Buy_80_percent_larger_than_sell_is_buy_heavy()
    {
        VolumePressure pressure = VolumePressure.Analyze(28, 18, 80);

        Assert.Equal(VolumePressureSide.BuyHeavy, pressure.Side);
        Assert.Equal(80m, pressure.ExcessPercent);
    }

    [Theory]
    [InlineData(27.9, 10)]   // bán lớn hơn 79%
    [InlineData(20, 10)]     // cân bằng
    [InlineData(27.9, 17.9)] // mua lớn hơn 79%
    public void Below_threshold_is_balanced(double volume, double takerBuy)
    {
        VolumePressure pressure = VolumePressure.Analyze((decimal)volume, (decimal)takerBuy, 80);

        Assert.Equal(VolumePressureSide.Balanced, pressure.Side);
        Assert.False(pressure.IsAlert);
    }

    [Fact]
    public void One_sided_volume_alerts_without_excess_percent()
    {
        VolumePressure allSell = VolumePressure.Analyze(5, 0, 80);
        VolumePressure allBuy = VolumePressure.Analyze(5, 5, 80);

        Assert.Equal(VolumePressureSide.SellHeavy, allSell.Side);
        Assert.Null(allSell.ExcessPercent);
        Assert.Equal(VolumePressureSide.BuyHeavy, allBuy.Side);
        Assert.Equal(100m, allBuy.BuyPercent);
    }

    [Fact]
    public void Empty_candle_is_balanced()
    {
        VolumePressure pressure = VolumePressure.Analyze(0, 0, 80);

        Assert.Equal(VolumePressureSide.Balanced, pressure.Side);
        Assert.Equal(50m, pressure.BuyPercent);
    }

    [Fact]
    public void Threshold_is_configurable()
    {
        Assert.Equal(VolumePressureSide.SellHeavy, VolumePressure.Analyze(25, 10, 50).Side);
        Assert.Equal(VolumePressureSide.Balanced, VolumePressure.Analyze(25, 10, 80).Side);
    }

    [Fact]
    public void Rejects_taker_buy_outside_volume()
    {
        Assert.Throws<ArgumentException>(() => VolumePressure.Analyze(5, 6, 80));
        Assert.Throws<ArgumentOutOfRangeException>(() => VolumePressure.Analyze(5, 2, 0));
    }

    [Fact]
    public void Candle_without_taker_buy_volume_has_no_pressure()
    {
        Candle old = new("Binance", "BTCUSDT", CandleInterval.H1, TestCandles.T0, 10, 10, 10, 10, 4, true,
            TestCandles.T0);
        Candle withTaker = new("Binance", "BTCUSDT", CandleInterval.H1, TestCandles.T0, 10, 10, 10, 10, 4, true,
            TestCandles.T0, takerBuyVolume: 1);

        Assert.Null(VolumePressure.From(old));
        Assert.Equal(VolumePressureSide.SellHeavy, VolumePressure.From(withTaker)!.Side);
    }
}
