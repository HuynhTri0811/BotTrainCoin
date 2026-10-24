using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Prediction;

namespace CryptoBot.Domain.Tests;

public class PriceDirectionPredictorTests
{
    private static readonly PredictorSettings Small = new() { TrendPeriod = 5, MomentumPeriod = 2 };

    [Fact]
    public void Rising_prices_predict_up()
    {
        PricePrediction p = PriceDirectionPredictor.Predict(TestCandles.FromCloses(100, 101, 102, 103, 104, 105), Small);

        Assert.Equal(PriceDirection.Up, p.Direction);
        Assert.True(p.Score > 0);
    }

    [Fact]
    public void Falling_prices_predict_down()
    {
        PricePrediction p = PriceDirectionPredictor.Predict(TestCandles.FromCloses(105, 104, 103, 102, 101, 100), Small);

        Assert.Equal(PriceDirection.Down, p.Direction);
        Assert.True(p.Score < 0);
    }

    [Fact]
    public void Flat_prices_still_answer_up_or_down()
    {
        PricePrediction p = PriceDirectionPredictor.Predict(TestCandles.FromCloses(100, 100, 100, 100, 100), Small);

        Assert.Equal(PriceDirection.Up, p.Direction);
        Assert.Equal(0m, p.Score);
    }

    [Fact]
    public void Sell_pressure_tips_a_flat_market_down()
    {
        List<Candle> candles = TestCandles.FromCloses(100, 100, 100, 100);
        candles.Add(new Candle("Binance", "BTCUSDT", CandleInterval.H1, TestCandles.T0 + TimeSpan.FromHours(4),
            100, 100, 100, 100, 10, true, TestCandles.T0, takerBuyVolume: 2));

        Assert.Equal(PriceDirection.Down, PriceDirectionPredictor.Predict(candles, Small).Direction);
    }

    [Fact]
    public void Zero_weights_disable_components()
    {
        PredictorSettings onlyTrend = Small with { MomentumWeight = 0, PressureWeight = 0 };
        // Ba nến cuối giảm (đà âm) nhưng giá vẫn trên trung bình (xu hướng dương).
        List<Candle> candles = TestCandles.FromCloses(90, 90, 120, 115, 110);

        Assert.Equal(PriceDirection.Down, PriceDirectionPredictor.Predict(candles, Small with { TrendWeight = 0 }).Direction);
        Assert.Equal(PriceDirection.Up, PriceDirectionPredictor.Predict(candles, onlyTrend).Direction);
    }

    [Fact]
    public void Rejects_too_few_or_running_candles()
    {
        Assert.Throws<ArgumentException>(() => PriceDirectionPredictor.Predict(TestCandles.FromCloses(1, 2, 3), Small));

        List<Candle> withRunning = TestCandles.FromCloses(1, 2, 3, 4);
        withRunning.Add(TestCandles.At(4, 5, closed: false));
        Assert.Throws<ArgumentException>(() => PriceDirectionPredictor.Predict(withRunning, Small));
    }

    [Fact]
    public void Backtest_counts_hits_using_only_past_candles()
    {
        // Tăng đều: mọi lần đoán đều Up và đều đúng.
        List<Candle> rising = TestCandles.FromCloses(Enumerable.Range(100, 20).Select(x => (decimal)x).ToArray());

        PredictionAccuracy result = PriceDirectionPredictor.Backtest(rising, Small);

        Assert.Equal(15, result.Predictions);
        Assert.Equal(15, result.Correct);
        Assert.Equal(100m, result.HitRatePercent);
    }

    [Fact]
    public void Backtest_skips_unchanged_closes()
    {
        PredictionAccuracy result = PriceDirectionPredictor.Backtest(TestCandles.FromCloses(1, 1, 1, 1, 1, 1, 1), Small);

        Assert.Equal(0, result.Predictions);
        Assert.Equal(0m, result.HitRatePercent);
    }
}
