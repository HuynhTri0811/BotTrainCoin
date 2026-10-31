using CryptoBot.Domain.Futures;

namespace CryptoBot.Domain.Tests;

public class FuturesPositionTests
{
    // 1 BTC vào 100, 10x → margin 10. Phí mở 0.05.
    private static FuturesPosition Open(PositionSide side = PositionSide.Long) =>
        new(Guid.NewGuid(), "BTCUSDT", side, 10, 1m, 100m, 10m, side == PositionSide.Long ? 90.36m : 109.56m,
            side == PositionSide.Long ? 95m : 105m, null, 0.05m, TestCandles.T0);

    [Fact]
    public void New_position_is_open_in_paper_without_exit()
    {
        var position = Open();

        Assert.Equal(PositionStatus.Open, position.Status);
        Assert.Equal("Paper", position.Environment);
        Assert.Null(position.CloseReason);
        Assert.Null(position.ExitPrice);
        Assert.Null(position.RealizedPnl);
        Assert.Null(position.ClosedAtUtc);
    }

    [Theory]
    [InlineData(PositionSide.Long, 104, 4)]
    [InlineData(PositionSide.Long, 97, -3)]
    [InlineData(PositionSide.Short, 97, 3)]
    [InlineData(PositionSide.Short, 104, -4)]
    public void Close_records_price_pnl_by_side(PositionSide side, int exit, int expectedPnl)
    {
        var position = Open(side);
        var at = TestCandles.T0.AddHours(1);
        var command = Guid.NewGuid();

        position.Close(CloseReason.Manual, exit, 0.04m, command, at);

        Assert.Equal(PositionStatus.Closed, position.Status);
        Assert.Equal(CloseReason.Manual, position.CloseReason);
        Assert.Equal(exit, position.ExitPrice);
        Assert.Equal(expectedPnl, position.RealizedPnl);
        Assert.Equal(command, position.CloseCommandId);
        Assert.Equal(at, position.ClosedAtUtc);
    }

    [Fact]
    public void Net_pnl_subtracts_open_and_close_fees()
    {
        var position = Open();
        Assert.Equal(-0.05m, position.NetPnl);

        position.Close(CloseReason.TakeProfit, 104m, 0.04m, null, TestCandles.T0);

        Assert.Equal(4m - 0.05m - 0.04m, position.NetPnl);
    }

    [Fact]
    public void Liquidation_loses_exactly_the_margin_without_close_fee()
    {
        var position = Open(PositionSide.Short);

        position.Liquidate(110m, TestCandles.T0);

        Assert.Equal(PositionStatus.Liquidated, position.Status);
        Assert.Equal(CloseReason.Liquidation, position.CloseReason);
        Assert.Equal(110m, position.ExitPrice);
        Assert.Equal(-10m, position.RealizedPnl);
        Assert.Equal(0m, position.CloseFee);
        Assert.Null(position.CloseCommandId);
    }

    [Fact]
    public void Closed_position_cannot_be_closed_again()
    {
        var position = Open();
        position.Close(CloseReason.StopLoss, 95m, 0.04m, null, TestCandles.T0);

        Assert.Throws<InvalidOperationException>(() =>
            position.Close(CloseReason.Manual, 96m, 0.04m, Guid.NewGuid(), TestCandles.T0));
    }

    [Fact]
    public void Ledger_entry_keeps_sign_type_and_position()
    {
        var position = Open();

        var fee = new PaperLedgerEntry(-position.OpenFee, LedgerEntryType.Fee, position, "Phí mở vị thế",
            TestCandles.T0);
        var deposit = new PaperLedgerEntry(1000m, LedgerEntryType.Deposit, null, "Vốn ban đầu", TestCandles.T0);

        Assert.Equal(-0.05m, fee.Amount);
        Assert.Same(position, fee.Position);
        Assert.Equal("USDT", fee.Asset);
        Assert.Equal("Paper", fee.Environment);
        Assert.Null(deposit.Position);
        Assert.Equal(LedgerEntryType.Deposit, deposit.Type);
    }
}
