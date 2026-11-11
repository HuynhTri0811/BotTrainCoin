namespace CryptoBot.Domain.Backtesting;

/// <summary>Một lệnh Long đã đóng trong backtest. Giá và tiền theo đồng quote (USDT).</summary>
/// <param name="EntryTimeUtc">Giờ mở của nến khớp lệnh vào (nến ngay sau nến phát BUY).</param>
/// <param name="EntryPrice">Giá vào = giá mở của nến đó.</param>
/// <param name="ExitTimeUtc">Giờ mở của nến khớp lệnh ra.</param>
/// <param name="ExitPrice">Giá ra (giá mở nến kế tiếp với SELL; mức TP/SL hoặc giá mở nếu nhảy qua mức).</param>
/// <param name="Quantity">Số coin đã mua.</param>
/// <param name="Fees">Tổng phí hai lần khớp.</param>
/// <param name="NetPnl">Lãi/lỗ sau phí.</param>
/// <param name="ReturnPercent">Lãi/lỗ sau phí so với số tiền vào lệnh, theo %.</param>
/// <param name="ExitReason">Vì sao đóng lệnh.</param>
public sealed record BacktestTrade(
    DateTime EntryTimeUtc,
    decimal EntryPrice,
    DateTime ExitTimeUtc,
    decimal ExitPrice,
    decimal Quantity,
    decimal Fees,
    decimal NetPnl,
    decimal ReturnPercent,
    BacktestExitReason ExitReason)
{
    /// <summary>Lệnh thắng khi còn lãi sau phí.</summary>
    public bool IsWin => NetPnl > 0;
}
