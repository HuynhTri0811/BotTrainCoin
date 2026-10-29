using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Tests;

public class StrategyInstanceTests
{
    private const string Params = """{"fastPeriod":9,"slowPeriod":21,"lookbackCandles":300}""";

    private static StrategyInstance Create() =>
        new("btc-1h-ema", "EmaCross", Params, "Binance", "BTCUSDT", "1h", enabled: true);

    [Fact]
    public void New_instance_starts_at_version_1_without_checkpoint()
    {
        var instance = Create();

        Assert.Equal(1, instance.Version);
        Assert.True(instance.Enabled);
        Assert.Null(instance.LastEvaluatedCandleOpenTimeUtc);
    }

    [Fact]
    public void Same_configuration_is_not_a_change()
    {
        var instance = Create();

        Assert.False(instance.Configure("EmaCross", Params, "Binance", "BTCUSDT", "1h", enabled: true));
        Assert.Equal(1, instance.Version);
    }

    [Fact]
    public void Toggling_enabled_is_a_change_but_keeps_version_and_checkpoint()
    {
        var instance = Create();
        instance.MarkEvaluated(TestCandles.T0);

        Assert.True(instance.Configure("EmaCross", Params, "Binance", "BTCUSDT", "1h", enabled: false));

        Assert.False(instance.Enabled);
        Assert.Equal(1, instance.Version);
        Assert.Equal(TestCandles.T0, instance.LastEvaluatedCandleOpenTimeUtc);
    }

    [Theory]
    [InlineData("EmaCross", "Binance", "ETHUSDT", "1h")]
    [InlineData("EmaCross", "Binance", "BTCUSDT", "15m")]
    [InlineData("EmaCross", "Bitfinex", "BTCUSDT", "1h")]
    [InlineData("Other", "Binance", "BTCUSDT", "1h")]
    public void Changing_type_exchange_symbol_or_interval_bumps_version(string type, string exchange, string symbol,
        string interval)
    {
        var instance = Create();
        instance.MarkEvaluated(TestCandles.T0);

        Assert.True(instance.Configure(type, Params, exchange, symbol, interval, enabled: true));

        Assert.Equal(2, instance.Version);
        Assert.Null(instance.LastEvaluatedCandleOpenTimeUtc);
    }

    [Fact]
    public void Checkpoint_only_moves_forward()
    {
        var instance = Create();
        var next = TestCandles.T0.AddHours(1);

        instance.MarkEvaluated(TestCandles.T0);
        instance.MarkEvaluated(next);

        Assert.Equal(next, instance.LastEvaluatedCandleOpenTimeUtc);
        Assert.Throws<InvalidOperationException>(() => instance.MarkEvaluated(TestCandles.T0));
    }

    [Fact]
    public void Trading_signal_cannot_be_hold()
    {
        Assert.Throws<ArgumentException>(() => new TradingSignal(1, 1, "BTCUSDT", "1h", TestCandles.T0,
            SignalAction.Hold, "x", 100m, 1m, 1m, TestCandles.T0, isBackfill: false));
    }

    [Fact]
    public void Trading_signal_keeps_backfill_flag_and_values()
    {
        var signal = new TradingSignal(1, 2, "BTCUSDT", "1h", TestCandles.T0, SignalAction.Buy,
            "EMA9 cắt lên EMA21.", 100m, 101m, 99m, TestCandles.T0.AddHours(5), isBackfill: true);

        Assert.Equal(SignalAction.Buy, signal.Action);
        Assert.Equal(2, signal.ConfigVersion);
        Assert.Equal(101m, signal.FastEma);
        Assert.Equal(99m, signal.SlowEma);
        Assert.True(signal.IsBackfill);
    }
}
