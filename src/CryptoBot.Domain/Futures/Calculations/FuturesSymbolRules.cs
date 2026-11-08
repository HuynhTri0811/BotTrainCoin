namespace CryptoBot.Domain.Futures;

/// <summary>Quy tắc giao dịch của một symbol (lấy từ exchangeInfo, cần kiểm tra lại khi tích hợp testnet).</summary>
/// <param name="Symbol">Mã cặp giao dịch, ví dụ <c>BTCUSDT</c>.</param>
/// <param name="QuantityStep">Bước nhảy nhỏ nhất của khối lượng (stepSize). Khối lượng đặt lệnh phải là bội số của giá trị này.</param>
/// <param name="TickSize">Bước nhảy nhỏ nhất của giá. Giá đặt lệnh phải là bội số của giá trị này.</param>
/// <param name="MinNotional">Giá trị lệnh tối thiểu (khối lượng × giá) tính bằng USDT.</param>
/// <param name="MaintenanceMarginRate">Tỷ lệ ký quỹ duy trì (mmr), dạng thập phân, ví dụ <c>0.004</c> = 0,4%.</param>
public sealed record FuturesSymbolRules(
    string Symbol,
    decimal QuantityStep,
    decimal TickSize,
    decimal MinNotional,
    decimal MaintenanceMarginRate);
