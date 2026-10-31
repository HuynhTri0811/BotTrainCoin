using CryptoBot.Domain.Futures;

namespace CryptoBot.Domain.Tests;

public class AutoOrderRuleTests
{
    private static AutoOrderRule PriceRule(TriggerCondition condition, decimal triggerPrice = 100m) =>
        new("BTCUSDT", PositionSide.Long, condition, triggerPrice, 50m, 2, 1m, 2m, TestCandles.T0);

    private static AutoOrderRule IndicatorRule(bool closeOnReverse = false) =>
        AutoOrderRule.ForIndicator("BTCUSDT", PositionSide.Long, "btc-1h-myrsi", 50m, 2, 1m, 2m, TestCandles.T0,
            closeOnReverse);

    [Theory]
    [InlineData(TriggerCondition.AtOrBelow, 99, true)]
    [InlineData(TriggerCondition.AtOrBelow, 100, true)]
    [InlineData(TriggerCondition.AtOrBelow, 101, false)]
    [InlineData(TriggerCondition.AtOrAbove, 101, true)]
    [InlineData(TriggerCondition.AtOrAbove, 100, true)]
    [InlineData(TriggerCondition.AtOrAbove, 99, false)]
    public void Price_rule_is_met_at_or_beyond_trigger_price(TriggerCondition condition, int mark, bool expected)
    {
        Assert.Equal(expected, PriceRule(condition).IsMet(mark));
    }

    [Fact]
    public void Indicator_rule_is_never_met_by_price()
    {
        var rule = IndicatorRule();

        Assert.False(rule.IsMet(0m));
        Assert.False(rule.IsMet(1_000_000m));
    }

    [Fact]
    public void New_rules_are_active_with_their_own_command_id()
    {
        var rule = PriceRule(TriggerCondition.AtOrBelow);
        var secondRule = PriceRule(TriggerCondition.AtOrBelow);

        Assert.Equal(AutoRuleStatus.Active, rule.Status);
        Assert.Equal("Paper", rule.Environment);
        Assert.NotEqual(Guid.Empty, rule.CommandId);
        Assert.NotEqual(rule.CommandId, secondRule.CommandId);
    }

    [Fact]
    public void Indicator_rule_requires_a_name_and_has_no_price_condition()
    {
        var rule = IndicatorRule();

        Assert.Equal(RuleTriggerType.Indicator, rule.TriggerType);
        Assert.Equal("btc-1h-myrsi", rule.IndicatorName);
        Assert.Null(rule.Condition);
        Assert.Null(rule.TriggerPrice);
        Assert.Throws<ArgumentException>(() =>
            AutoOrderRule.ForIndicator("BTCUSDT", PositionSide.Long, " ", 50m, 2, 1m, 2m, TestCandles.T0));
    }

    [Fact]
    public void Close_on_reverse_gets_a_separate_close_command_id()
    {
        var watching = IndicatorRule(closeOnReverse: true);
        var plain = IndicatorRule();

        Assert.NotNull(watching.CloseCommandId);
        Assert.NotEqual(watching.CommandId, watching.CloseCommandId);
        Assert.Null(plain.CloseCommandId);
    }

    [Fact]
    public void Triggering_records_position_and_clears_blocked_note()
    {
        var rule = PriceRule(TriggerCondition.AtOrBelow);
        rule.SetBlockedNote("Bị chặn");
        var at = TestCandles.T0.AddMinutes(5);

        rule.MarkTriggered(99.5m, positionId: 7, at, "RSI 31");

        Assert.Equal(AutoRuleStatus.Triggered, rule.Status);
        Assert.Equal(99.5m, rule.TriggeredMarkPrice);
        Assert.Equal(7, rule.PositionId);
        Assert.Equal(at, rule.TriggeredAtUtc);
        Assert.Equal("RSI 31", rule.TriggerDetail);
        Assert.Null(rule.Note);
    }

    [Fact]
    public void Rejecting_keeps_the_reason_and_no_position()
    {
        var rule = PriceRule(TriggerCondition.AtOrBelow);

        rule.MarkRejected(99m, "Không đủ số dư.", TestCandles.T0);

        Assert.Equal(AutoRuleStatus.Rejected, rule.Status);
        Assert.Equal("Không đủ số dư.", rule.Note);
        Assert.Equal(99m, rule.TriggeredMarkPrice);
        Assert.Null(rule.PositionId);
    }

    [Fact]
    public void Rule_that_is_no_longer_active_cannot_change_again()
    {
        var triggered = PriceRule(TriggerCondition.AtOrBelow);
        triggered.MarkTriggered(99m, 1, TestCandles.T0);
        var cancelled = PriceRule(TriggerCondition.AtOrBelow);
        cancelled.Cancel();

        Assert.Equal(AutoRuleStatus.Cancelled, cancelled.Status);
        foreach (var rule in new[] { triggered, cancelled })
        {
            Assert.Throws<InvalidOperationException>(() => rule.MarkTriggered(99m, 2, TestCandles.T0));
            Assert.Throws<InvalidOperationException>(() => rule.MarkRejected(99m, "x", TestCandles.T0));
            Assert.Throws<InvalidOperationException>(() => rule.Cancel());
            Assert.Throws<InvalidOperationException>(() => rule.SetBlockedNote("x"));
        }
    }

    [Fact]
    public void Blocked_note_reports_whether_it_changed()
    {
        var rule = PriceRule(TriggerCondition.AtOrBelow);

        Assert.True(rule.SetBlockedNote("Bị chặn"));
        Assert.False(rule.SetBlockedNote("Bị chặn"));
        Assert.True(rule.SetBlockedNote(null));
        Assert.Null(rule.Note);
    }

    [Fact]
    public void Watches_for_reverse_only_after_triggering_until_the_watch_ends()
    {
        var rule = IndicatorRule(closeOnReverse: true);
        Assert.False(rule.IsWatchingClose);

        rule.MarkTriggered(100m, 1, TestCandles.T0);
        Assert.True(rule.IsWatchingClose);

        var at = TestCandles.T0.AddHours(3);
        rule.EndCloseWatch("Chỉ báo đảo chiều", at);

        Assert.False(rule.IsWatchingClose);
        Assert.Equal(at, rule.CloseWatchEndedAtUtc);
        Assert.Equal("Chỉ báo đảo chiều", rule.CloseDetail);
        Assert.Throws<InvalidOperationException>(() => rule.EndCloseWatch("lần hai", at));
    }

    [Fact]
    public void Rule_without_close_on_reverse_never_watches()
    {
        var rule = IndicatorRule();
        rule.MarkTriggered(100m, 1, TestCandles.T0);

        Assert.False(rule.IsWatchingClose);
        Assert.Throws<InvalidOperationException>(() => rule.EndCloseWatch("x", TestCandles.T0));
    }
}
