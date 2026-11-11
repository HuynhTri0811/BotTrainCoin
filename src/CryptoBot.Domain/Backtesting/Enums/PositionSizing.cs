namespace CryptoBot.Domain.Backtesting;

/// <summary>Cách tính số tiền vào mỗi lệnh khi backtest.</summary>
public enum PositionSizing
{
    /// <summary>Một phần trăm của tiền mặt lúc vào lệnh (ví dụ 10% tổng vốn).</summary>
    PercentOfEquity = 0,

    /// <summary>Một số tiền cố định theo đồng quote (ví dụ 100 USDT mỗi lệnh).</summary>
    FixedAmount = 1,
}
