using CryptoBot.Domain.Futures;

namespace CryptoBot.Domain.Tests;

public class FuturesMathTests
{
    [Fact]
    public void Long_liquidation_price_satisfies_maintenance_margin_equation()
    {
        // entry 100, 10x, mmr 0.4%: P = 100 * 0.9 / 0.996
        var liq = FuturesMath.LiquidationPrice(PositionSide.Long, 100m, 10, 0.004m);

        Assert.Equal(90.3614m, Math.Round(liq, 4));
        // qty 1, margin 10: margin + PnL = mmr * qty * P
        Assert.Equal(Math.Round(0.004m * liq, 10), Math.Round(10m + (liq - 100m), 10));
    }

    [Fact]
    public void Short_liquidation_price_satisfies_maintenance_margin_equation()
    {
        var liq = FuturesMath.LiquidationPrice(PositionSide.Short, 100m, 10, 0.004m);

        Assert.Equal(109.5618m, Math.Round(liq, 4));
        Assert.Equal(Math.Round(0.004m * liq, 10), Math.Round(10m + (100m - liq), 10));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(5, 80.3213)]
    [InlineData(10, 90.3614)]
    public void Higher_leverage_moves_liquidation_closer(int leverage, double expected)
    {
        var liq = FuturesMath.LiquidationPrice(PositionSide.Long, 100m, leverage, 0.004m);

        Assert.Equal((decimal)expected, Math.Round(liq, 4));
    }

    [Fact]
    public void Pnl_sign_depends_on_side()
    {
        Assert.Equal(2m, FuturesMath.UnrealizedPnl(PositionSide.Long, 0.5m, 100m, 104m));
        Assert.Equal(-2m, FuturesMath.UnrealizedPnl(PositionSide.Short, 0.5m, 100m, 104m));
    }

    [Theory]
    [InlineData(PositionSide.Long, true, 100.05)]
    [InlineData(PositionSide.Long, false, 99.95)]
    [InlineData(PositionSide.Short, true, 99.95)]
    [InlineData(PositionSide.Short, false, 100.05)]
    public void Slippage_is_always_adverse(PositionSide side, bool opening, double expected)
    {
        Assert.Equal((decimal)expected, FuturesMath.ApplySlippage(100m, side, opening, 0.0005m));
    }

    [Fact]
    public void Rounding_follows_step_and_tick()
    {
        Assert.Equal(0.012m, FuturesMath.RoundDown(0.0129m, 0.001m));
        Assert.Equal(81958.8m, FuturesMath.RoundToTick(81958.75066667m, 0.1m));
    }

    [Fact]
    public void Zero_step_or_tick_leaves_value_unchanged()
    {
        Assert.Equal(0.0129m, FuturesMath.RoundDown(0.0129m, 0m));
        Assert.Equal(81958.75m, FuturesMath.RoundToTick(81958.75m, 0m));
    }

    [Fact]
    public void Tick_rounding_goes_away_from_zero_at_midpoint()
    {
        Assert.Equal(100.2m, FuturesMath.RoundToTick(100.15m, 0.1m));
        Assert.Equal(100.1m, FuturesMath.RoundToTick(100.14m, 0.1m));
    }

    [Theory]
    [InlineData(PositionSide.Long, 90, true)]
    [InlineData(PositionSide.Long, 89, true)]
    [InlineData(PositionSide.Long, 91, false)]
    [InlineData(PositionSide.Short, 110, true)]
    [InlineData(PositionSide.Short, 111, true)]
    [InlineData(PositionSide.Short, 109, false)]
    public void Liquidation_triggers_at_or_beyond_the_price(PositionSide side, int mark, bool expected)
    {
        var liquidation = side == PositionSide.Long ? 90m : 110m;

        Assert.Equal(expected, FuturesMath.IsLiquidated(side, mark, liquidation));
    }

    [Theory]
    [InlineData(PositionSide.Long, 95, true, false)]
    [InlineData(PositionSide.Long, 105, false, true)]
    [InlineData(PositionSide.Long, 100, false, false)]
    [InlineData(PositionSide.Short, 105, true, false)]
    [InlineData(PositionSide.Short, 95, false, true)]
    [InlineData(PositionSide.Short, 100, false, false)]
    public void Stop_and_take_profit_are_hit_on_the_side_that_loses_or_wins(PositionSide side, int mark,
        bool stopHit, bool takeProfitHit)
    {
        // Long: SL 95 dưới giá vào, TP 105 trên. Short: ngược lại.
        var (stop, takeProfit) = side == PositionSide.Long ? (95m, 105m) : (105m, 95m);

        Assert.Equal(stopHit, FuturesMath.StopHit(side, mark, stop));
        Assert.Equal(takeProfitHit, FuturesMath.TakeProfitHit(side, mark, takeProfit));
    }

    [Fact]
    public void Isolated_close_loss_is_capped_at_margin()
    {
        var p = new FuturesPosition(Guid.NewGuid(), "BTCUSDT", PositionSide.Long, 10, 1m, 100m, 10m, 90.36m,
            95m, null, 0.05m, TestCandles.T0);

        p.Close(CloseReason.StopLoss, 80m, 0.04m, null, TestCandles.T0);

        Assert.Equal(-10m, p.RealizedPnl);
        Assert.Throws<InvalidOperationException>(() => p.Liquidate(80m, TestCandles.T0));
    }
}
