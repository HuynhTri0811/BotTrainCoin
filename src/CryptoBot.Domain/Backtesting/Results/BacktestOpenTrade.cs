namespace CryptoBot.Domain.Backtesting;

/// <summary>Lệnh còn mở ở nến cuối của backtest; không tính vào số lệnh, tỷ lệ thắng hay lợi nhuận ròng.</summary>
/// <param name="EntryTimeUtc">Giờ mở của nến khớp lệnh vào.</param>
/// <param name="EntryPrice">Giá vào.</param>
/// <param name="Quantity">Số coin đang giữ.</param>
/// <param name="EntryFee">Phí đã trả lúc vào.</param>
/// <param name="MarkPrice">Giá đóng cửa của nến cuối.</param>
/// <param name="UnrealizedPnl">Lãi/lỗ tạm tính theo <paramref name="MarkPrice"/>, đã trừ phí vào (chưa trừ phí ra).</param>
public sealed record BacktestOpenTrade(
    DateTime EntryTimeUtc,
    decimal EntryPrice,
    decimal Quantity,
    decimal EntryFee,
    decimal MarkPrice,
    decimal UnrealizedPnl);
