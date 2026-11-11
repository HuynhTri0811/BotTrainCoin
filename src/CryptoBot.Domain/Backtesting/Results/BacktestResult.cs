namespace CryptoBot.Domain.Backtesting;

/// <summary>Kết quả đầy đủ của một lần backtest.</summary>
/// <param name="Settings">Tham số vốn/rủi ro đã dùng.</param>
/// <param name="Statistics">Bảng thống kê.</param>
/// <param name="Trades">Các lệnh đã đóng, theo thứ tự thời gian.</param>
/// <param name="OpenTrade">Lệnh còn mở ở nến cuối, nếu có.</param>
/// <param name="Signals">Mọi tín hiệu BUY/SELL chiến lược phát ra (kể cả khi TP/SL đã đóng lệnh trước đó).</param>
/// <param name="Equity">Tổng vốn tại từng nến được đánh giá.</param>
/// <param name="EvaluatedCandles">Số nến chiến lược được hỏi ý kiến (đủ cửa sổ, không thiếu nến).</param>
/// <param name="SkippedGapWindows">Số nến bị bỏ qua vì cửa sổ có nến thiếu.</param>
public sealed record BacktestResult(
    BacktestSettings Settings,
    BacktestStatistics Statistics,
    IReadOnlyList<BacktestTrade> Trades,
    BacktestOpenTrade? OpenTrade,
    IReadOnlyList<BacktestSignal> Signals,
    IReadOnlyList<EquityPoint> Equity,
    int EvaluatedCandles,
    int SkippedGapWindows);
