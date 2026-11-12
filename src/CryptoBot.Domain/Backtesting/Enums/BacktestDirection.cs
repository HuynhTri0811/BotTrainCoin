namespace CryptoBot.Domain.Backtesting;

/// <summary>Hướng lệnh được phép mở khi backtest Futures.</summary>
public enum BacktestDirection
{
    /// <summary>Chỉ mở Long; tín hiệu Short chỉ dùng để đóng Long (khi bật đóng lệnh khi đảo chiều).</summary>
    LongOnly = 0,

    /// <summary>Chỉ mở Short; tín hiệu Long chỉ dùng để đóng Short (khi bật đóng lệnh khi đảo chiều).</summary>
    ShortOnly = 1,

    /// <summary>Mở cả hai chiều; tín hiệu ngược chiều đóng lệnh đang giữ rồi mở chiều kia (khi bật).</summary>
    Both = 2,
}
