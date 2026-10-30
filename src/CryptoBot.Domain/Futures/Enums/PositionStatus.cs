namespace CryptoBot.Domain.Futures;

/// <summary>Trạng thái vị thế.</summary>
public enum PositionStatus
{
    /// <summary>Đang mở, còn được kiểm tra SL/TP/thanh lý.</summary>
    Open = 1,

    /// <summary>Đã đóng (tay, chạm SL hoặc chạm TP).</summary>
    Closed = 2,

    /// <summary>Bị thanh lý: mất toàn bộ margin.</summary>
    Liquidated = 3,
}
