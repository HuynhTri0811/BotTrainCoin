namespace CryptoBot.Domain.Backtesting;

/// <summary>
/// Quản lý vốn và rủi ro cho một lần backtest. Chiến lược vẫn quyết định BUY/SELL; các tham số này chỉ quyết định
/// vào bao nhiêu tiền, thoát sớm ở đâu và mất bao nhiêu phí.
/// </summary>
/// <param name="InitialCapital">Vốn ban đầu theo đồng quote, ví dụ 1000 USDT.</param>
/// <param name="Sizing">Tính tiền mỗi lệnh theo % vốn hay số tiền cố định.</param>
/// <param name="SizeValue">% vốn (0–100] hoặc số tiền cố định, tùy <paramref name="Sizing"/>.</param>
/// <param name="TakeProfitPercent">Chốt lời khi giá tăng bấy nhiêu % so với giá vào; null = không dùng.</param>
/// <param name="StopLossPercent">Cắt lỗ khi giá giảm bấy nhiêu % so với giá vào; null = không dùng.</param>
/// <param name="FeePercent">Phí mỗi lần khớp lệnh, tính trên giá trị lệnh, ví dụ 0.1 (%).</param>
public sealed record BacktestSettings(
    decimal InitialCapital,
    PositionSizing Sizing,
    decimal SizeValue,
    decimal? TakeProfitPercent,
    decimal? StopLossPercent,
    decimal FeePercent)
{
    /// <summary>Kiểm tra tham số; trả về lý do nếu sai, null nếu hợp lệ.</summary>
    public string? Validate()
    {
        if (InitialCapital <= 0)
            return "Vốn ban đầu phải > 0.";
        if (SizeValue <= 0)
            return "Khối lượng mỗi lệnh phải > 0.";
        if (Sizing == PositionSizing.PercentOfEquity && SizeValue > 100)
            return "Khối lượng theo % vốn phải trong khoảng (0, 100].";
        if (TakeProfitPercent is <= 0)
            return "Chốt lời phải > 0%.";
        // Cắt lỗ 100% nghĩa là giá về 0: không còn là mức cắt lỗ.
        if (StopLossPercent is <= 0 or >= 100)
            return "Cắt lỗ phải trong khoảng (0, 100)%.";
        if (FeePercent is < 0 or >= 100)
            return "Phí phải trong khoảng [0, 100)%.";
        return null;
    }
}
