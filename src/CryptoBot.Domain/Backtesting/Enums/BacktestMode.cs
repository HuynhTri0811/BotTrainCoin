namespace CryptoBot.Domain.Backtesting;

/// <summary>Kiểu giao dịch được giả lập.</summary>
public enum BacktestMode
{
    /// <summary>Mua coin bằng tiền mặt rồi bán lại: chỉ Long, không đòn bẩy, như bot EMA đang chạy.</summary>
    Spot = 0,

    /// <summary>Hợp đồng USDⓈ-M isolated: Long/Short, đòn bẩy, phí, trượt giá, thanh lý như Paper Futures.</summary>
    Futures = 1,
}
