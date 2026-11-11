namespace CryptoBot.Domain.Backtesting;

/// <summary>Vì sao một lệnh backtest được đóng.</summary>
public enum BacktestExitReason
{
    /// <summary>Chiến lược phát SELL; đóng ở giá mở nến kế tiếp.</summary>
    Signal = 0,

    /// <summary>Giá chạm mức chốt lời.</summary>
    TakeProfit = 1,

    /// <summary>Giá chạm mức cắt lỗ.</summary>
    StopLoss = 2,
}
