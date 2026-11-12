using CryptoBot.Domain.Strategies;

namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Kết quả một lần backtest đã lưu. Id, CreatedAtUtc, Note là của bản lưu; Settings là vốn/rủi ro đã dùng.
/// FirstCandleUtc/LastCandleUtc là khoảng nến được giao dịch; WarmupCandles nến trước đó chỉ để có đủ cửa sổ.
/// EvaluatedCandles là số nến chiến lược được hỏi, SkippedGapWindows là số nến bỏ qua vì cửa sổ thiếu nến.
/// Parameters là tham số EMA (null khi tín hiệu là chỉ báo tự viết); Signal là nguồn tín hiệu. Futures: BuySignals là
/// số tín hiệu Long, SellSignals là số tín hiệu Short.
/// </summary>
public sealed record BacktestDto(
    long Id,
    DateTime CreatedAtUtc,
    string? Note,
    int StrategyId,
    string Symbol,
    string Interval,
    EmaCrossParameters? Parameters,
    BacktestSignalDto Signal,
    BacktestSettingsDto Settings,
    DateTime? FirstCandleUtc,
    DateTime? LastCandleUtc,
    int CandleCount,
    int WarmupCandles,
    int EvaluatedCandles,
    int SkippedGapWindows,
    int BuySignals,
    int SellSignals,
    BacktestStatisticsDto Statistics,
    IReadOnlyList<BacktestTradeDto> Trades,
    BacktestOpenTradeDto? OpenTrade,
    BacktestChartDto Chart);
