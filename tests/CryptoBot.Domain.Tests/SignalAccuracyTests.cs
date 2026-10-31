using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Prediction;

namespace CryptoBot.Domain.Tests;

public class SignalAccuracyTests
{
    private static readonly List<Candle> Candles = TestCandles.FromCloses(100, 110, 99, 99, 120);

    [Fact]
    public void Counts_hits_per_horizon_in_signal_direction()
    {
        List<DirectionSignal> signals = [new(0, PriceDirection.Up), new(1, PriceDirection.Down)];

        IReadOnlyList<HorizonAccuracy> results = SignalAccuracy.Evaluate(Candles, signals, [1, 3]);

        // Sau 1 nến: Up 100→110 đúng (+10%), Down 110→99 đúng (+10%).
        Assert.Equal(2, results[0].Signals);
        Assert.Equal(2, results[0].Correct);
        Assert.Equal(10m, results[0].AverageMovePercent);
        // Sau 3 nến: Up 100→99 sai (−1%), Down 110→120 sai.
        Assert.Equal(0, results[1].Correct);
        Assert.Equal(0m, results[1].HitRatePercent);
        Assert.True(results[1].AverageMovePercent < 0);
    }

    [Fact]
    public void Signals_without_enough_later_candles_or_unchanged_price_are_skipped()
    {
        List<DirectionSignal> signals = [new(2, PriceDirection.Up), new(4, PriceDirection.Down)];

        HorizonAccuracy accuracy = SignalAccuracy.Evaluate(Candles, signals, [1])[0];

        // 99→99 không đổi; nến cuối chưa có nến sau.
        Assert.Equal(0, accuracy.Signals);
        Assert.Null(SignalAccuracy.MovePercent(Candles, new DirectionSignal(4, PriceDirection.Up), 1));
    }

    [Fact]
    public void Rejects_bad_horizon_or_index()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SignalAccuracy.Evaluate(Candles, [], [0]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SignalAccuracy.Evaluate(Candles, [new DirectionSignal(9, PriceDirection.Up)], [1]));
    }
}
