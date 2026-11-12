using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.Futures;
using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Tests;

public class FuturesBacktesterTests
{
    /// <summary>Nguồn tín hiệu cố định theo vị trí nến, để tính tay từng lệnh.</summary>
    private sealed class ScriptedSignals(Dictionary<int, PositionSide> signals) : IBacktestSignalSource
    {
        public int RequiredCandles => 1;

        public BacktestSignalReading Read(IReadOnlyList<Candle> window)
        {
            int index = (int)((window[^1].OpenTimeUtc - TestCandles.T0) / CandleInterval.H1.Duration);
            return signals.TryGetValue(index, out PositionSide side)
                ? new BacktestSignalReading(side, $"{side} ở nến {index}")
                : BacktestSignalReading.None;
        }
    }

    private static BacktestSettings Settings(BacktestDirection direction = BacktestDirection.Both, int leverage = 2,
        bool closeOnReverse = true, decimal? takeProfit = null, decimal? stopLoss = null, decimal fee = 0,
        decimal slippage = 0, decimal maintenance = 0.4m, PositionSizing sizing = PositionSizing.PercentOfEquity,
        decimal size = 100) =>
        new(1000m, sizing, size, takeProfit, stopLoss, fee,
            new FuturesBacktestSettings(leverage, direction, closeOnReverse, slippage, maintenance));

    private static BacktestResult Run(IReadOnlyList<Candle> candles, BacktestSettings settings,
        params (int Index, PositionSide Side)[] signals) =>
        FuturesBacktester.Run(candles, CandleInterval.H1,
            new ScriptedSignals(signals.ToDictionary(signal => signal.Index, signal => signal.Side)), settings);

    private static Candle Bar(int index, decimal open, decimal high, decimal low, decimal close) =>
        new("Binance", "BTCUSDT", CandleInterval.H1, HourOf(index), open, high, low, close, 1m, true, TestCandles.T0);

    private static DateTime HourOf(int index) => TestCandles.T0 + TimeSpan.FromHours(index);

    private static decimal Round(decimal value) => Math.Round(value, 8);

    [Fact]
    public void Long_enters_and_exits_at_the_next_open_with_leverage()
    {
        // Long ở nến 1 → vào ở giá mở nến 2 (100); Short ở nến 4 → đóng ở giá mở nến 5 (110).
        BacktestResult result = Run(TestCandles.FromCloses(100, 100, 100, 100, 110, 110),
            Settings(BacktestDirection.LongOnly), (1, PositionSide.Long), (4, PositionSide.Short));

        // Margin 1000 × 2x = 2000 USDT → 20 coin; lãi 20 × 10 = 200 = 20% margin.
        BacktestTrade trade = Assert.Single(result.Trades);
        Assert.Equal(PositionSide.Long, trade.Side);
        Assert.Equal((HourOf(2), 100m, HourOf(5), 110m), (trade.EntryTimeUtc, trade.EntryPrice, trade.ExitTimeUtc,
            trade.ExitPrice));
        Assert.Equal(20m, Round(trade.Quantity));
        Assert.Equal(200m, Round(trade.NetPnl));
        Assert.Equal(20m, Round(trade.ReturnPercent));
        Assert.Equal(BacktestExitReason.Signal, trade.ExitReason);
        Assert.Null(result.OpenTrade);
        Assert.Equal(1200m, Round(result.Statistics.EndingEquity));
    }

    [Fact]
    public void Short_profits_when_the_price_falls_and_both_directions_flip_on_the_opposite_signal()
    {
        BacktestResult result = Run(TestCandles.FromCloses(100, 100, 100, 95, 90, 90, 99),
            Settings(), (1, PositionSide.Short), (4, PositionSide.Long));

        // Short 20 coin ở 100, đóng ở 90: lãi 200. Cùng giá mở đó mở Long với margin 1200.
        BacktestTrade trade = Assert.Single(result.Trades);
        Assert.Equal(PositionSide.Short, trade.Side);
        Assert.Equal(200m, Round(trade.NetPnl));
        BacktestOpenTrade open = Assert.IsType<BacktestOpenTrade>(result.OpenTrade);
        Assert.Equal((PositionSide.Long, HourOf(5), 90m), (open.Side, open.EntryTimeUtc, open.EntryPrice));
        // 1200 × 2 / 90 coin, giá đóng nến cuối 99.
        Assert.Equal(Round(2400m / 90 * 9), Round(open.UnrealizedPnl));
        Assert.Equal([SignalAction.Sell, SignalAction.Buy], result.Signals.Select(signal => signal.Action));
    }

    [Fact]
    public void Short_only_ignores_long_entries_but_closes_on_them()
    {
        BacktestResult result = Run(TestCandles.FromCloses(100, 100, 100, 100, 100, 100, 100, 100),
            Settings(BacktestDirection.ShortOnly), (1, PositionSide.Long), (3, PositionSide.Short),
            (5, PositionSide.Long));

        BacktestTrade trade = Assert.Single(result.Trades);
        Assert.Equal((PositionSide.Short, HourOf(4), HourOf(6)), (trade.Side, trade.EntryTimeUtc, trade.ExitTimeUtc));
        Assert.Null(result.OpenTrade);
    }

    [Fact]
    public void Same_side_signal_while_holding_does_not_add_a_second_position()
    {
        BacktestResult result = Run(TestCandles.FromCloses(100, 100, 100, 100, 100, 100),
            Settings(), (1, PositionSide.Long), (3, PositionSide.Long));

        Assert.Empty(result.Trades);
        Assert.Equal(HourOf(2), Assert.IsType<BacktestOpenTrade>(result.OpenTrade).EntryTimeUtc);
    }

    [Fact]
    public void Without_close_on_reverse_the_opposite_signal_is_ignored_until_the_stop()
    {
        List<Candle> candles =
        [
            .. TestCandles.FromCloses(100, 100, 100, 100, 100),
            Bar(5, 100, 100, 96, 98),
        ];

        BacktestResult result = Run(candles, Settings(closeOnReverse: false, stopLoss: 3),
            (1, PositionSide.Long), (3, PositionSide.Short));

        BacktestTrade trade = Assert.Single(result.Trades);
        Assert.Equal((BacktestExitReason.StopLoss, HourOf(5), 97m), (trade.ExitReason, trade.ExitTimeUtc,
            trade.ExitPrice));
        Assert.Null(result.OpenTrade);
    }

    [Fact]
    public void Short_stop_loss_is_above_the_entry_and_take_profit_below()
    {
        List<Candle> stopped = [.. TestCandles.FromCloses(100, 100, 100), Bar(3, 100, 106, 99, 104)];
        List<Candle> taken = [.. TestCandles.FromCloses(100, 100, 100), Bar(3, 100, 101, 89, 92)];
        BacktestSettings settings = Settings(BacktestDirection.ShortOnly, takeProfit: 10, stopLoss: 5);

        BacktestTrade stop = Assert.Single(Run(stopped, settings, (1, PositionSide.Short)).Trades);
        BacktestTrade take = Assert.Single(Run(taken, settings, (1, PositionSide.Short)).Trades);

        Assert.Equal((BacktestExitReason.StopLoss, 105m, -100m), (stop.ExitReason, stop.ExitPrice,
            Round(stop.NetPnl)));
        Assert.Equal((BacktestExitReason.TakeProfit, 90m, 200m), (take.ExitReason, take.ExitPrice,
            Round(take.NetPnl)));
    }

    [Fact]
    public void A_candle_touching_both_stop_and_target_counts_as_the_stop()
    {
        List<Candle> candles = [.. TestCandles.FromCloses(100, 100, 100), Bar(3, 100, 111, 96, 100)];

        BacktestTrade trade = Assert.Single(Run(candles, Settings(takeProfit: 10, stopLoss: 3),
            (1, PositionSide.Long)).Trades);

        Assert.Equal(BacktestExitReason.StopLoss, trade.ExitReason);
    }

    [Fact]
    public void Liquidation_loses_the_whole_margin_and_the_entry_fee()
    {
        // 10x, ký quỹ duy trì 0.4%: giá thanh lý Long = 100 × 0.9 / 0.996 ≈ 90.36.
        List<Candle> candles = [.. TestCandles.FromCloses(100, 100, 100), Bar(3, 99, 99, 85, 95)];
        BacktestSettings settings = Settings(leverage: 10, fee: 0.05m, sizing: PositionSizing.FixedAmount, size: 100);

        BacktestResult result = Run(candles, settings, (1, PositionSide.Long));

        // Margin 100, giá trị vị thế 1000, phí mở 0.5; không có phí đóng.
        BacktestTrade trade = Assert.Single(result.Trades);
        Assert.Equal(BacktestExitReason.Liquidation, trade.ExitReason);
        Assert.Equal(Round(90m / 0.996m), Round(trade.ExitPrice));
        Assert.Equal(-100.5m, Round(trade.NetPnl));
        Assert.Equal(0.5m, Round(trade.Fees));
        Assert.Equal(899.5m, Round(result.Statistics.EndingEquity));
    }

    [Fact]
    public void Opening_beyond_the_liquidation_price_liquidates_at_the_open_before_the_stop()
    {
        List<Candle> candles = [.. TestCandles.FromCloses(100, 100, 100, 100), Bar(4, 120, 125, 118, 121)];

        BacktestTrade trade = Assert.Single(Run(candles, Settings(BacktestDirection.ShortOnly, leverage: 10,
            stopLoss: 5), (1, PositionSide.Short)).Trades);

        Assert.Equal((BacktestExitReason.Liquidation, 120m), (trade.ExitReason, trade.ExitPrice));
    }

    [Fact]
    public void A_gap_past_the_liquidation_price_liquidates_before_the_signal_close()
    {
        // Long 10x ở 100 (thanh lý ≈ 90.36); Short ở nến 3 hẹn đóng ở giá mở nến 4, nhưng nến 4 mở ở 85.
        List<Candle> candles = [.. TestCandles.FromCloses(100, 100, 100, 100), Bar(4, 85, 86, 84, 85)];

        BacktestResult result = Run(candles, Settings(BacktestDirection.LongOnly, leverage: 10),
            (1, PositionSide.Long), (3, PositionSide.Short));

        BacktestTrade trade = Assert.Single(result.Trades);
        Assert.Equal((BacktestExitReason.Liquidation, 85m, -1000m), (trade.ExitReason, trade.ExitPrice,
            Round(trade.NetPnl)));
    }

    [Fact]
    public void Slippage_moves_both_fills_against_the_trader()
    {
        BacktestResult result = Run(TestCandles.FromCloses(100, 100, 100, 100, 110, 110),
            Settings(BacktestDirection.LongOnly, slippage: 0.1m), (1, PositionSide.Long), (4, PositionSide.Short));

        BacktestTrade trade = Assert.Single(result.Trades);
        Assert.Equal(100.1m, trade.EntryPrice);
        Assert.Equal(109.89m, trade.ExitPrice);
    }

    [Fact]
    public void Margin_and_opening_fee_never_exceed_the_cash()
    {
        BacktestResult result = Run(TestCandles.FromCloses(100, 100, 100, 100),
            Settings(leverage: 10, fee: 0.05m, sizing: PositionSizing.FixedAmount, size: 5000),
            (1, PositionSide.Long));

        // Margin m với m + m × 10 × 0.05% = 1000.
        BacktestOpenTrade open = Assert.IsType<BacktestOpenTrade>(result.OpenTrade);
        decimal margin = 1000m / 1.005m;
        Assert.Equal(Round(margin * 10 / 100), Round(open.Quantity));
        Assert.Equal(Round(1000m - margin), Round(open.EntryFee));
    }

    [Fact]
    public void Ema_cross_source_trades_both_crosses()
    {
        // EMA2/3 cửa sổ 4: cắt lên ở nến 3, cắt xuống ở nến 7 (giống BacktesterTests).
        EmaCrossSignalSource source = new(new EmaCrossParameters(2, 3, 4));

        BacktestResult result = FuturesBacktester.Run(TestCandles.FromCloses(10, 10, 10, 13, 14, 15, 16, 8, 7, 7),
            CandleInterval.H1, source, Settings(leverage: 1));

        BacktestTrade trade = Assert.Single(result.Trades);
        Assert.Equal((PositionSide.Long, 14m, 7m), (trade.Side, trade.EntryPrice, trade.ExitPrice));
        Assert.Equal((PositionSide.Short, 7m), (result.OpenTrade!.Side, result.OpenTrade.EntryPrice));
        Assert.Equal("EMA2 cắt xuống EMA3.", result.Signals[1].Reason);
    }

    [Fact]
    public void Indicator_source_maps_long_and_short_signals()
    {
        IndicatorSignalSource source = new(new LastCloseIndicator());

        Assert.Equal(PositionSide.Long, source.Read(TestCandles.FromCloses(1)).Side);
        Assert.Equal(PositionSide.Short, source.Read(TestCandles.FromCloses(2)).Side);
        Assert.Null(source.Read(TestCandles.FromCloses(3)).Side);
        Assert.Equal("close 2", source.Read(TestCandles.FromCloses(2)).Reason);
    }

    /// <summary>Giá đóng 1 → Long, 2 → Short, còn lại không có tín hiệu.</summary>
    private sealed class LastCloseIndicator : ICustomIndicator
    {
        public int RequiredCandles => 1;
        public IReadOnlyList<IndicatorLine> Lines => [];
        public string? Validate() => null;

        public IndicatorResult Compute(IReadOnlyList<Candle> closedCandles) => closedCandles[^1].Close switch
        {
            1m => new IndicatorResult(new Dictionary<string, decimal?>(), IndicatorSignal.Long, "close 1"),
            2m => new IndicatorResult(new Dictionary<string, decimal?>(), IndicatorSignal.Short, "close 2"),
            _ => IndicatorResult.None("none"),
        };
    }

    [Theory]
    [InlineData(BacktestDirection.LongOnly, 10, 9.7, "thanh lý")]
    [InlineData(BacktestDirection.ShortOnly, 10, 9.7, "thanh lý")]
    [InlineData(BacktestDirection.Both, 0, 3, "Đòn bẩy")]
    [InlineData(BacktestDirection.Both, 126, 3, "Đòn bẩy")]
    public void Rejects_bad_futures_settings(BacktestDirection direction, int leverage, double stopLoss,
        string message)
    {
        BacktestSettings settings = Settings(direction, leverage, stopLoss: (decimal)stopLoss);

        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            Run(TestCandles.FromCloses(100, 100), settings));
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void Needs_a_way_to_close_positions()
    {
        Assert.Contains("ít nhất một cách đóng lệnh", Settings(closeOnReverse: false).Validate());
        Assert.Null(Settings(closeOnReverse: false, takeProfit: 5).Validate());
    }

    [Fact]
    public void Spot_and_futures_engines_only_take_their_own_settings()
    {
        BacktestSettings spot = new(1000m, PositionSizing.PercentOfEquity, 100, null, null, 0);

        Assert.Throws<ArgumentException>(() => FuturesBacktester.Run(TestCandles.FromCloses(100), CandleInterval.H1,
            new ScriptedSignals(new Dictionary<int, PositionSide>()), spot));
        Assert.Throws<ArgumentException>(() => Backtester.Run(TestCandles.FromCloses(100), CandleInterval.H1,
            new EmaCrossStrategy(new EmaCrossParameters(2, 3, 4)), Settings()));
        Assert.Equal(BacktestMode.Spot, spot.Mode);
        Assert.Equal(BacktestMode.Futures, Settings().Mode);
    }
}
