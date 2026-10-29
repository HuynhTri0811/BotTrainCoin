namespace CryptoBot.Domain.Futures;

/// <summary>Lý do vị thế kết thúc.</summary>
public enum CloseReason
{
    /// <summary>Người dùng bấm đóng.</summary>
    Manual = 1,

    /// <summary>Mark price chạm stop loss.</summary>
    StopLoss = 2,

    /// <summary>Mark price chạm take profit.</summary>
    TakeProfit = 3,

    /// <summary>Mark price chạm giá thanh lý.</summary>
    Liquidation = 4,

    /// <summary>Quy tắc tự động đóng vì chỉ báo phát tín hiệu ngược hướng vị thế.</summary>
    Signal = 5,
}
