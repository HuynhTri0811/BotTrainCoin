using CryptoBot.Domain.Common;

namespace CryptoBot.Domain.Backtesting;

/// <summary>Thống kê của một lần backtest. Tiền theo đồng quote (USDT), tỷ lệ theo %.</summary>
/// <param name="TotalTrades">Số lệnh đã đóng (không tính lệnh còn mở ở nến cuối).</param>
/// <param name="Wins">Số lệnh còn lãi sau phí.</param>
/// <param name="Losses">Số lệnh hòa hoặc lỗ sau phí.</param>
/// <param name="WinRatePercent">Wins / TotalTrades × 100; 0 khi chưa có lệnh.</param>
/// <param name="NetProfit">Tổng lãi/lỗ sau phí của các lệnh đã đóng.</param>
/// <param name="NetProfitPercent">NetProfit so với vốn ban đầu.</param>
/// <param name="GrossProfit">Tổng lãi của các lệnh thắng (sau phí).</param>
/// <param name="GrossLoss">Tổng lỗ của các lệnh thua (sau phí), số dương.</param>
/// <param name="ProfitFactor">GrossProfit / GrossLoss; null khi chưa có lệnh lỗ.</param>
/// <param name="TotalFees">Tổng phí đã trả, gồm cả phí vào của lệnh còn mở.</param>
/// <param name="MaxDrawdown">Mức sụt giảm lớn nhất của tổng vốn từ đỉnh, theo tiền.</param>
/// <param name="MaxDrawdownPercent">Mức sụt giảm lớn nhất so với đỉnh ngay trước đó, theo %.</param>
/// <param name="EndingEquity">Tổng vốn ở nến cuối (tính cả lệnh còn mở theo giá đóng cửa).</param>
/// <param name="BuyAndHoldPercent">% thay đổi giá nếu mua ở nến đầu được đánh giá và giữ đến nến cuối, để so sánh.</param>
public sealed record BacktestStatistics(
    int TotalTrades,
    int Wins,
    int Losses,
    decimal WinRatePercent,
    decimal NetProfit,
    decimal NetProfitPercent,
    decimal GrossProfit,
    decimal GrossLoss,
    decimal? ProfitFactor,
    decimal TotalFees,
    decimal MaxDrawdown,
    decimal MaxDrawdownPercent,
    decimal EndingEquity,
    decimal BuyAndHoldPercent)
{
    /// <summary>Tính thống kê từ danh sách lệnh và đường vốn.</summary>
    /// <param name="initialCapital">Vốn ban đầu.</param>
    /// <param name="trades">Các lệnh đã đóng.</param>
    /// <param name="openTrade">Lệnh còn mở ở nến cuối, nếu có.</param>
    /// <param name="equity">Tổng vốn tại từng nến, tăng dần theo thời gian.</param>
    /// <param name="firstClose">Giá đóng cửa ở nến đầu tiên được đánh giá (mốc mua-và-giữ).</param>
    /// <param name="lastClose">Giá đóng cửa ở nến cuối.</param>
    public static BacktestStatistics From(decimal initialCapital, IReadOnlyList<BacktestTrade> trades,
        BacktestOpenTrade? openTrade, IReadOnlyList<EquityPoint> equity, decimal firstClose, decimal lastClose)
    {
        int wins = trades.Count(trade => trade.IsWin);
        decimal grossProfit = trades.Where(trade => trade.IsWin).Sum(trade => trade.NetPnl);
        decimal grossLoss = -trades.Where(trade => !trade.IsWin).Sum(trade => trade.NetPnl);
        decimal netProfit = grossProfit - grossLoss;
        decimal fees = trades.Sum(trade => trade.Fees) + (openTrade?.EntryFee ?? 0m);
        (decimal maxDrawdown, decimal maxDrawdownPercent) = MaxDrawdownOf(initialCapital, equity);

        return new BacktestStatistics(
            trades.Count,
            wins,
            trades.Count - wins,
            trades.Count == 0 ? 0m : (decimal)wins / trades.Count * 100,
            netProfit,
            netProfit / initialCapital * 100,
            grossProfit,
            grossLoss,
            grossLoss > 0 ? grossProfit / grossLoss : null,
            fees,
            maxDrawdown,
            maxDrawdownPercent,
            equity.Count == 0 ? initialCapital : equity[^1].Equity,
            firstClose > 0 ? PercentChange.Between(firstClose, lastClose) : 0m);
    }

    /// <summary>
    /// Sụt giảm lớn nhất từ đỉnh của tổng vốn. Đỉnh bắt đầu từ vốn ban đầu. Tiền và % được lấy cực đại riêng,
    /// nên có thể rơi vào hai đợt sụt giảm khác nhau (giống cách TradingView báo cáo).
    /// </summary>
    private static (decimal Amount, decimal Percent) MaxDrawdownOf(decimal initialCapital,
        IReadOnlyList<EquityPoint> equity)
    {
        decimal peak = initialCapital;
        decimal maxAmount = 0m;
        decimal maxPercent = 0m;
        foreach (EquityPoint point in equity)
        {
            peak = Math.Max(peak, point.Equity);
            decimal drawdown = peak - point.Equity;
            maxAmount = Math.Max(maxAmount, drawdown);
            maxPercent = Math.Max(maxPercent, drawdown / peak * 100);
        }

        return (maxAmount, maxPercent);
    }
}
