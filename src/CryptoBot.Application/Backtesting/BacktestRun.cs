using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Backtesting;

/// <summary>Kết quả backtest kèm ngữ cảnh: chạy trên instance nào, tín hiệu nào, khoảng nến nào.</summary>
/// <param name="Instance">Strategy instance (sàn/cặp/khung nến).</param>
/// <param name="Parameters">Tham số EMA đã dùng; null khi tín hiệu là chỉ báo tự viết.</param>
/// <param name="Signal">Nguồn tín hiệu và tham số đã dùng.</param>
/// <param name="FirstCandleUtc">Nến đầu tiên trong khoảng được giao dịch; null khi không có nến.</param>
/// <param name="LastCandleUtc">Nến cuối; null khi không có nến.</param>
/// <param name="CandleCount">Số nến trong khoảng (không tính nến warm-up phía trước).</param>
/// <param name="WarmupCandles">Số nến trước khoảng dùng để có đủ cửa sổ cho nến đầu.</param>
/// <param name="Result">Lệnh, tín hiệu và thống kê.</param>
/// <param name="Chart">Dữ liệu vẽ chart (đã gộp nến nếu quá nhiều).</param>
public sealed record BacktestRun(
    StrategyInstance Instance,
    EmaCrossParameters? Parameters,
    BacktestSignalSpec Signal,
    DateTime? FirstCandleUtc,
    DateTime? LastCandleUtc,
    int CandleCount,
    int WarmupCandles,
    BacktestResult Result,
    BacktestChart Chart);
