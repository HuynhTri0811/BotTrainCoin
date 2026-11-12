using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Application.Backtesting;

/// <summary>Yêu cầu chạy backtest cho một strategy instance.</summary>
/// <param name="StrategyId">Instance cho biết sàn/cặp/khung nến và tham số mặc định.</param>
/// <param name="FromUtc">Nến đầu tiên được giao dịch (null = từ nến sớm nhất trong DB).</param>
/// <param name="ToUtc">Nến cuối (null = đến nến đóng mới nhất).</param>
/// <param name="Overrides">Tham số EMA thay cho tham số của instance; null = dùng của instance.</param>
/// <param name="Settings">Vốn, khối lượng, TP/SL, phí; có <see cref="BacktestSettings.Futures"/> = Futures.</param>
/// <param name="SignalType">
/// Null hoặc "EmaCross" = EMA cross (với <paramref name="Overrides"/>); tên loại chỉ báo tự viết (ví dụ "MyRsi") = dùng
/// chỉ báo đó làm tín hiệu, chỉ với Futures.
/// </param>
/// <param name="SignalParameters">Tham số của chỉ báo; tham số không gửi giữ giá trị mặc định của class.</param>
public sealed record BacktestRequest(
    int StrategyId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    EmaCrossOverrides? Overrides,
    BacktestSettings Settings,
    string? SignalType = null,
    IReadOnlyDictionary<string, string?>? SignalParameters = null);
