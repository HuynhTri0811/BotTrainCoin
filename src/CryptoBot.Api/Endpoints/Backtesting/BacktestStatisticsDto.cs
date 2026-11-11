namespace CryptoBot.Api.Endpoints;

/// <summary>Thống kê backtest. Tiền theo đồng quote (USDT), tỷ lệ theo %. ProfitFactor null khi chưa có lệnh lỗ.</summary>
public sealed record BacktestStatisticsDto(
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
    decimal BuyAndHoldPercent);
