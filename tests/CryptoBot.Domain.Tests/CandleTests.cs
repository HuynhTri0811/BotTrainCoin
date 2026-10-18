using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Tests;

public class CandleTests
{
    [Fact]
    public void Validator_detects_gap_duplicate_out_of_order_and_invalid_values()
    {
        var bad = new Candle("Binance", "BTCUSDT", CandleInterval.H1, TestCandles.T0 + TimeSpan.FromHours(5),
            open: 10, high: 9, low: 8, close: 10, volume: 1, isClosed: true, receivedAtUtc: TestCandles.T0);
        List<Candle> candles =
        [
            TestCandles.At(0, 10),
            TestCandles.At(1, 10),
            TestCandles.At(1, 10), // trùng
            TestCandles.At(4, 10), // thiếu nến 2, 3
            TestCandles.At(3, 10), // sai thứ tự
            bad,
        ];

        var kinds = CandleSeriesValidator.Validate(candles, CandleInterval.H1).Select(i => i.Kind).ToList();

        Assert.Equal(
            [CandleIssueKind.Duplicate, CandleIssueKind.Gap, CandleIssueKind.OutOfOrder,
             CandleIssueKind.InvalidValues, CandleIssueKind.Gap],
            kinds);
    }

    [Fact]
    public void Continuous_series_has_no_issues()
    {
        Assert.Empty(CandleSeriesValidator.Validate(TestCandles.FromCloses(1, 2, 3), CandleInterval.H1));
    }

    [Fact]
    public void Open_candle_is_updated_by_newer_version()
    {
        var open = TestCandles.At(0, 10, closed: false);
        var closed = TestCandles.At(0, 12, closed: true);

        Assert.True(open.ApplyUpdate(closed));
        Assert.True(open.IsClosed);
        Assert.Equal(12m, open.Close);
    }

    [Fact]
    public void Closed_candle_is_never_overwritten()
    {
        var closed = TestCandles.At(0, 10);
        var different = TestCandles.At(0, 11);

        Assert.False(closed.ApplyUpdate(different));
        Assert.Equal(10m, closed.Close);
    }

    [Fact]
    public void Rejects_non_utc_timestamps()
    {
        Assert.Throws<ArgumentException>(() => new Candle("Binance", "BTCUSDT", CandleInterval.H1,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local), 1, 1, 1, 1, 1, true, TestCandles.T0));
    }

    [Fact]
    public void Strategy_instance_version_bumps_and_checkpoint_resets_on_parameter_change()
    {
        var s = new StrategyInstance("x", "EmaCross", "{\"a\":1}", "Binance", "BTCUSDT", "1h", true);
        s.MarkEvaluated(TestCandles.T0);

        s.Configure("EmaCross", "{\"a\":1}", "Binance", "BTCUSDT", "1h", enabled: false);
        Assert.Equal(1, s.Version);
        Assert.Equal(TestCandles.T0, s.LastEvaluatedCandleOpenTimeUtc);

        s.Configure("EmaCross", "{\"a\":2}", "Binance", "BTCUSDT", "1h", enabled: true);
        Assert.Equal(2, s.Version);
        Assert.Null(s.LastEvaluatedCandleOpenTimeUtc);
    }

    [Fact]
    public void Strategy_instance_rejects_evaluating_same_candle_twice()
    {
        var s = new StrategyInstance("x", "EmaCross", "{}", "Binance", "BTCUSDT", "1h", true);
        s.MarkEvaluated(TestCandles.T0);

        Assert.Throws<InvalidOperationException>(() => s.MarkEvaluated(TestCandles.T0));
    }
}
