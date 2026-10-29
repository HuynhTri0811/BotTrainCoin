namespace CryptoBot.Domain.Strategies;

/// <summary>Quyết định của chiến lược tại một nến đã đóng.</summary>
public enum SignalAction
{
    /// <summary>Không phát tín hiệu (không có giao cắt, chưa đủ nến, cửa sổ có nến thiếu…).</summary>
    Hold = 0,

    /// <summary>Vào lệnh mua; chỉ phát khi chưa giữ và không có lệnh vào đang chờ.</summary>
    Buy = 1,

    /// <summary>Thoát lệnh; chỉ phát khi đang giữ (chiến lược long-only).</summary>
    Sell = 2,
}
