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
    public void Rejected_issues_remove_their_candles_but_gaps_keep_them()
    {
        List<Candle> candles =
        [
            TestCandles.At(0, 10),
            TestCandles.At(1, 10),
            TestCandles.At(1, 11), // trùng: cả hai nến 1 bị loại
            TestCandles.At(4, 10), // sau khoảng thiếu: vẫn giữ
            TestCandles.At(3, 10), // sai thứ tự: bị loại
        ];
        var issues = CandleSeriesValidator.Validate(candles, CandleInterval.H1);

        var kept = CandleSeriesValidator.WithoutRejected(candles, issues);

        Assert.Equal([TestCandles.T0, TestCandles.T0.AddHours(4)], kept.Select(c => c.OpenTimeUtc));
        Assert.False(issues.Single(i => i.Kind == CandleIssueKind.Gap).RejectsCandle);
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

    private static Candle WithStats(bool closed, decimal close = 10, long? trades = 5) =>
        new("Binance", "BTCUSDT", CandleInterval.H1, TestCandles.T0, 10, Math.Max(10, close), Math.Min(10, close),
            close, 1, closed, TestCandles.T0, quoteVolume: trades is null ? null : 10m, tradeCount: trades);

    [Fact]
    public void Closed_candle_without_stats_is_filled_from_same_candle()
    {
        var stored = WithStats(closed: true, trades: null);

        Assert.True(stored.ApplyUpdate(WithStats(closed: true, trades: 42)));
        Assert.Equal(42, stored.TradeCount);
        Assert.Equal(10m, stored.QuoteVolume);
    }

    [Fact]
    public void Closed_candle_keeps_stats_when_exchange_returns_different_prices()
    {
        var stored = WithStats(closed: true, trades: null);

        Assert.False(stored.ApplyUpdate(WithStats(closed: true, close: 11, trades: 42)));
        Assert.Null(stored.TradeCount);
    }

    [Fact]
    public void Running_candle_takes_latest_stats()
    {
        var stored = WithStats(closed: false, trades: 5);

        Assert.True(stored.ApplyUpdate(WithStats(closed: true, trades: 9)));
        Assert.Equal(9, stored.TradeCount);
    }

    [Fact]
    public void Closed_candle_without_taker_buy_volume_is_filled_from_same_candle()
    {
        Candle stored = WithStats(closed: true);
        Candle newer = new("Binance", "BTCUSDT", CandleInterval.H1, TestCandles.T0, 10, 10, 10, 10, 1, true,
            TestCandles.T0, quoteVolume: 10m, tradeCount: 5, takerBuyVolume: 0.4m);

        Assert.True(stored.ApplyUpdate(newer));
        Assert.Equal(0.4m, stored.TakerBuyVolume);
    }

    [Fact]
    public void Taker_buy_volume_above_volume_is_invalid()
    {
        Candle c = new("Binance", "BTCUSDT", CandleInterval.H1, TestCandles.T0, 10, 10, 10, 10, 1, true,
            TestCandles.T0, takerBuyVolume: 2);

        Assert.Equal("Khối lượng mua chủ động phải nằm trong [0, Volume].", c.Validate());
    }

    [Fact]
    public void Negative_trade_count_is_invalid()
    {
        Assert.Equal("Số giao dịch âm.", WithStats(closed: true, trades: -1).Validate());
    }

    private static Candle Ohlcv(decimal open, decimal high, decimal low, decimal close, decimal volume = 1) =>
        new("Binance", "BTCUSDT", CandleInterval.H1, TestCandles.T0, open, high, low, close, volume, true,
            TestCandles.T0);

    [Theory]
    [InlineData(0, 10, 5, 8)]
    [InlineData(8, 10, 0, 8)]
    [InlineData(8, 10, 5, -1)]
    public void Non_positive_prices_are_invalid(int open, int high, int low, int close)
    {
        Assert.Equal("Giá phải dương.", Ohlcv(open, high, low, close).Validate());
    }

    [Fact]
    public void Negative_volume_is_invalid()
    {
        Assert.Equal("Volume âm.", Ohlcv(8, 10, 5, 9, volume: -1).Validate());
    }

    [Theory]
    [InlineData(8, 9, 5, 10)]  // High dưới Close
    [InlineData(11, 10, 5, 9)] // High dưới Open
    [InlineData(8, 10, 9, 9)]  // Low trên Open
    [InlineData(8, 10, 6, 5)]  // Low trên Close
    public void High_and_low_must_contain_open_and_close(int open, int high, int low, int close)
    {
        Assert.Equal("High/Low không bao Open/Close.", Ohlcv(open, high, low, close).Validate());
    }

    [Fact]
    public void Valid_candle_has_no_reason_and_close_time_follows_interval()
    {
        var c = Ohlcv(8, 10, 5, 9);

        Assert.Null(c.Validate());
        Assert.Equal(TestCandles.T0.AddHours(1), c.CloseTimeUtc);
        Assert.Equal("1h", c.Interval);
    }

    [Fact]
    public void Gap_issue_says_how_many_candles_are_missing()
    {
        List<Candle> candles = [TestCandles.At(0, 10), TestCandles.At(3, 10)];

        var issue = Assert.Single(CandleSeriesValidator.Validate(candles, CandleInterval.H1));

        Assert.Equal(CandleIssueKind.Gap, issue.Kind);
        Assert.Equal(TestCandles.At(3, 10).OpenTimeUtc, issue.OpenTimeUtc);
        Assert.StartsWith("Thiếu khoảng 2 nến", issue.Detail);
    }

    [Fact]
    public void Update_from_a_different_candle_is_a_programming_error()
    {
        var stored = TestCandles.At(0, 10, closed: false);

        Assert.Throws<InvalidOperationException>(() => stored.ApplyUpdate(TestCandles.At(1, 10)));
    }
}
