namespace CryptoBot.Domain.Futures;

/// <summary>Loại bút toán trong sổ vốn giả lập.</summary>
public enum LedgerEntryType
{
    /// <summary>Nạp vốn (vốn giả lập ban đầu).</summary>
    Deposit = 1,

    /// <summary>Phí giao dịch (số âm).</summary>
    Fee = 2,

    /// <summary>Lãi/lỗ đã chốt khi đóng hoặc bị thanh lý.</summary>
    RealizedPnl = 3,
}
