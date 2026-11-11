using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Tests;

public class BacktesterTests
{
    // Fast=2, Slow=3, cửa sổ 4 nến: giống EmaCrossStrategyTests, đủ nhỏ để tính tay.
    private static readonly EmaCrossStrategy Strategy = new(new EmaCrossParameters(2, 3, 4));

    // BUY ở nến 3 (close 13), SELL ở nến 7 (close 8).
    private static readonly decimal[] UpThenDown = [10, 10, 10, 13, 14, 15, 16, 8, 7, 7];

    // BUY 3, SELL 7, BUY 11, SELL 14.
    private static readonly decimal[] TwoRounds = [10, 10, 10, 13, 14, 15, 16, 8, 7, 7, 7, 12, 13, 14, 6, 6];

    // BUY ở nến 3, không bao giờ SELL.
    private static readonly decimal[] OnlyUp = [10, 10, 10, 13, 14, 15, 16, 17, 18, 19];

    private static BacktestSettings Settings(decimal sizePercent = 100, decimal? takeProfit = null,
        decimal? stopLoss = null, decimal fee = 0) =>
        new(1000m, PositionSizing.PercentOfEquity, sizePercent, takeProfit, stopLoss, fee);

    private static BacktestResult Run(IReadOnlyList<Candle> candles, BacktestSettings settings) =>
        Backtester.Run(candles, CandleInterval.H1, Strategy, settings);

    private static Candle Bar(int index, decimal open, decimal high, decimal low, decimal close) =>
        new("Binance", "BTCUSDT", CandleInterval.H1, TestCandles.T0 + TimeSpan.FromHours(index),
            open, high, low, close, 1m, true, TestCandles.T0);

    private static DateTime HourOf(int index) => TestCandles.T0 + TimeSpan.FromHours(index);

    [Fact]
    public void Enters_and_exits_at_the_next_candle_open_after_each_signal()
    {
        BacktestResult result = Run(TestCandles.FromCloses(UpThenDown), Settings());

        BacktestTrade trade = Assert.Single(result.Trades);
        Assert.Equal(HourOf(4), trade.EntryTimeUtc);
        Assert.Equal(14m, trade.EntryPrice);
        Assert.Equal(HourOf(8), trade.ExitTimeUtc);
        Assert.Equal(7m, trade.ExitPrice);
        Assert.Equal(BacktestExitReason.Signal, trade.ExitReason);
        Assert.Equal(-500m, Math.Round(trade.NetPnl, 10));
        Assert.Equal(-50m, Math.Round(trade.ReturnPercent, 10));
    }

    [Fact]
    public void Records_the_same_signals_as_the_bot()
    {
        BacktestResult result = Run(TestCandles.FromCloses(TwoRounds), Settings());

        Assert.Equal(
            [(HourOf(3), SignalAction.Buy), (HourOf(7), SignalAction.Sell), (HourOf(11), SignalAction.Buy),
                (HourOf(14), SignalAction.Sell)],
            result.Signals.Select(signal => (signal.CandleOpenTimeUtc, signal.Action)));
        Assert.Equal("EMA2 cắt lên EMA3.", result.Signals[0].Reason);
        Assert.Equal(2, result.Trades.Count);
    }

    [Fact]
    public void Charges_the_fee_on_both_fills_and_sizes_by_percent_of_cash()
    {
        BacktestResult result = Run(TestCandles.FromCloses(UpThenDown), Settings(sizePercent: 10, fee: 0.1m));

        // Vào 100 USDT ở 14 (phí 0.1), bán ở 7 được 50 (phí 0.05).
        BacktestTrade trade = Assert.Single(result.Trades);
        Assert.Equal(0.15m, Math.Round(trade.Fees, 10));
        Assert.Equal(-50.15m, Math.Round(trade.NetPnl, 10));
        Assert.Equal(949.85m, Math.Round(result.Statistics.EndingEquity, 10));
        Assert.Equal(0.15m, Math.Round(result.Statistics.TotalFees, 10));
    }

    [Fact]
    public void Fixed_amount_never_spends_more_than_the_cash()
    {
        BacktestSettings settings = new(1000m, PositionSizing.FixedAmount, 5000m, null, null, 0.1m);

        BacktestResult result = Run(TestCandles.FromCloses(UpThenDown), settings);

        // Cả vốn kể cả phí: 1000 / 1.001 vào lệnh, tiền mặt về 0.
        BacktestTrade trade = Assert.Single(result.Trades);
        Assert.Equal(Math.Round(1000m / 1.001m / 14m, 10), Math.Round(trade.Quantity, 10));
    }

    [Fact]
    public void Stop_loss_touched_inside_a_candle_fills_at_the_stop_price()
    {
        List<Candle> candles = TestCandles.FromCloses(UpThenDown);
        candles[5] = Bar(5, 14, 15, 12, 15);

        BacktestResult result = Run(candles, Settings(stopLoss: 10));

        BacktestTrade trade = result.Trades[0];
        Assert.Equal(BacktestExitReason.StopLoss, trade.ExitReason);
        Assert.Equal(HourOf(5), trade.ExitTimeUtc);
        Assert.Equal(12.6m, trade.ExitPrice);
    }

    [Fact]
    public void Gap_through_the_stop_fills_at_the_worse_open()
    {
        // Nến 7 mở thẳng ở 8, dưới mức cắt lỗ 12.6.
        BacktestResult result = Run(TestCandles.FromCloses(UpThenDown), Settings(stopLoss: 10));

        BacktestTrade trade = Assert.Single(result.Trades);
        Assert.Equal(BacktestExitReason.StopLoss, trade.ExitReason);
        Assert.Equal(8m, trade.ExitPrice);
        // SELL ở nến 7 vẫn được ghi dù lệnh đã đóng bởi SL.
        Assert.Contains(result.Signals, signal => signal.Action == SignalAction.Sell);
    }

    [Fact]
    public void Candle_touching_both_take_profit_and_stop_loss_counts_as_stop_loss()
    {
        List<Candle> candles = TestCandles.FromCloses(UpThenDown);
        candles[5] = Bar(5, 14, 16, 12, 15);

        BacktestResult result = Run(candles, Settings(takeProfit: 10, stopLoss: 10));

        Assert.Equal(BacktestExitReason.StopLoss, result.Trades[0].ExitReason);
        Assert.Equal(12.6m, result.Trades[0].ExitPrice);
    }

    [Fact]
    public void Take_profit_closes_early_but_keeps_the_signal_sequence()
    {
        BacktestResult withoutTakeProfit = Run(TestCandles.FromCloses(TwoRounds), Settings());
        BacktestResult withTakeProfit = Run(TestCandles.FromCloses(TwoRounds), Settings(takeProfit: 5));

        Assert.Equal(withoutTakeProfit.Signals, withTakeProfit.Signals);
        Assert.All(withTakeProfit.Trades, trade => Assert.Equal(BacktestExitReason.TakeProfit, trade.ExitReason));
        // Mở lại chỉ ở BUY kế tiếp (nến 11 → khớp nến 12), không mở ngay sau khi chốt lời.
        Assert.Equal([HourOf(4), HourOf(12)], withTakeProfit.Trades.Select(trade => trade.EntryTimeUtc));
        // Nến 5 mở ở 15, trên mức 14.7: khớp ở giá mở.
        Assert.Equal(15m, withTakeProfit.Trades[0].ExitPrice);
    }

    [Fact]
    public void Position_still_open_at_the_end_is_reported_but_not_counted()
    {
        BacktestResult result = Run(TestCandles.FromCloses(OnlyUp), Settings());

        Assert.Empty(result.Trades);
        Assert.Equal(0, result.Statistics.TotalTrades);
        Assert.Equal(0m, result.Statistics.NetProfit);
        BacktestOpenTrade open = Assert.IsType<BacktestOpenTrade>(result.OpenTrade);
        Assert.Equal(14m, open.EntryPrice);
        Assert.Equal(19m, open.MarkPrice);
        Assert.Equal(Math.Round(1000m / 14m * 19m - 1000m, 10), Math.Round(open.UnrealizedPnl, 10));
        Assert.Equal(Math.Round(1000m / 14m * 19m, 10), Math.Round(result.Statistics.EndingEquity, 10));
    }

    [Fact]
    public void Windows_with_missing_candles_are_skipped()
    {
        // Thiếu nến lúc 06:00: 3 cửa sổ 4 nến chứa khoảng trống (kết thúc ở 07:00, 08:00, 09:00) bị bỏ qua.
        List<Candle> candles = OnlyUp
            .Select((close, index) => TestCandles.At(index < 6 ? index : index + 1, close))
            .ToList();

        BacktestResult result = Run(candles, Settings());

        Assert.Equal(3, result.SkippedGapWindows);
        Assert.Equal(4, result.EvaluatedCandles);
    }

    [Fact]
    public void Statistics_cover_win_rate_drawdown_and_buy_and_hold()
    {
        BacktestResult result = Run(TestCandles.FromCloses(UpThenDown), Settings());
        BacktestStatistics statistics = result.Statistics;

        Assert.Equal(1, statistics.TotalTrades);
        Assert.Equal(0, statistics.Wins);
        Assert.Equal(1, statistics.Losses);
        Assert.Equal(0m, statistics.WinRatePercent);
        Assert.Equal(-50m, Math.Round(statistics.NetProfitPercent, 10));
        Assert.Equal(0m, statistics.ProfitFactor);
        // Đỉnh 1000/14×16 ≈ 1142.86 ở nến 6, đáy 500 sau khi bán ở 7: giảm 56.25%.
        Assert.Equal(Math.Round(1000m / 14m * 16m - 500m, 10), Math.Round(statistics.MaxDrawdown, 10));
        Assert.Equal(56.25m, Math.Round(statistics.MaxDrawdownPercent, 10));
        // Mua-và-giữ từ nến đầu được đánh giá (close 13) đến nến cuối (close 7).
        Assert.Equal(Math.Round((7m / 13m - 1) * 100, 10), Math.Round(statistics.BuyAndHoldPercent, 10));
    }

    [Theory]
    [InlineData(0, 10, null, null, 0.1)]
    [InlineData(1000, 0, null, null, 0.1)]
    [InlineData(1000, 101, null, null, 0.1)]
    [InlineData(1000, 10, 0.0, null, 0.1)]
    [InlineData(1000, 10, null, 100.0, 0.1)]
    [InlineData(1000, 10, null, null, -1)]
    public void Invalid_settings_are_rejected(double capital, double size, double? takeProfit, double? stopLoss,
        double fee)
    {
        BacktestSettings settings = new((decimal)capital, PositionSizing.PercentOfEquity, (decimal)size,
            (decimal?)takeProfit, (decimal?)stopLoss, (decimal)fee);

        Assert.NotNull(settings.Validate());
        Assert.Throws<ArgumentException>(() => Run(TestCandles.FromCloses(UpThenDown), settings));
    }
}
