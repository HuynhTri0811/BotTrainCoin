using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Tests;

public class EmaCrossStrategyTests
{
    // Fast=2, Slow=3, cửa sổ 4 nến: đủ nhỏ để tính tay.
    private static readonly EmaCrossStrategy Strategy = new(new EmaCrossParameters(2, 3, 4));

    private static StrategyDecision Evaluate(decimal[] closes, bool hasPosition = false, bool pendingEntry = false) =>
        Strategy.Evaluate(new StrategyContext(TestCandles.FromCloses(closes), hasPosition, pendingEntry, 1));

    [Fact]
    public void Buy_on_upward_cross()
    {
        // fast: prev 10, cur 2/3*13 + 1/3*10 = 12; slow: prev 10, cur 0.5*13 + 0.5*10 = 11.5
        var decision = Evaluate([10, 10, 10, 13]);

        Assert.Equal(SignalAction.Buy, decision.Action);
        Assert.Equal(new IndicatorValues(10m, 10m, 12m, 11.5m), decision.Indicators);
    }

    [Fact]
    public void Sell_on_downward_cross_when_holding()
    {
        var decision = Evaluate([10, 10, 10, 7], hasPosition: true);

        Assert.Equal(SignalAction.Sell, decision.Action);
    }

    [Fact]
    public void Downward_cross_without_position_is_hold()
    {
        var decision = Evaluate([10, 10, 10, 7], hasPosition: false);

        Assert.Equal(SignalAction.Hold, decision.Action);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Upward_cross_with_position_or_pending_entry_is_hold(bool hasPosition, bool pendingEntry)
    {
        var decision = Evaluate([10, 10, 10, 13], hasPosition, pendingEntry);

        Assert.Equal(SignalAction.Hold, decision.Action);
    }

    [Fact]
    public void No_cross_is_hold()
    {
        // Fast đã ở trên slow từ nến trước: không phải giao cắt mới.
        var decision = Evaluate([10, 11, 12, 13]);

        Assert.Equal(SignalAction.Hold, decision.Action);
    }

    [Fact]
    public void Insufficient_history_is_hold_without_indicators()
    {
        var decision = Evaluate([10, 10, 13]);

        Assert.Equal(SignalAction.Hold, decision.Action);
        Assert.Null(decision.Indicators);
        Assert.Contains("Warm-up", decision.Reason);
    }

    [Fact]
    public void Uses_only_last_lookback_candles()
    {
        // Thêm nến cũ phía trước không được làm đổi kết quả.
        var decision = Evaluate([500, 1, 10, 10, 10, 13]);

        Assert.Equal(new IndicatorValues(10m, 10m, 12m, 11.5m), decision.Indicators);
    }

    [Fact]
    public void Rejects_unclosed_candles()
    {
        var candles = TestCandles.FromCloses(10, 10, 10);
        candles.Add(TestCandles.At(3, 13, closed: false));

        Assert.Throws<ArgumentException>(() =>
            Strategy.Evaluate(new StrategyContext(candles, false, false, 1)));
    }

    [Theory]
    [InlineData(9, 9, 300)]
    [InlineData(0, 21, 300)]
    [InlineData(9, 21, 21)]
    public void Invalid_parameters_are_rejected(int fast, int slow, int lookback)
    {
        Assert.Throws<ArgumentException>(() => new EmaCrossStrategy(new EmaCrossParameters(fast, slow, lookback)));
    }
}
