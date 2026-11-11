using CryptoBot.Domain.Strategies;

namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Kết quả một lần backtest đã lưu. Id, CreatedAtUtc, Note là của bản lưu; Settings là vốn/rủi ro đã dùng.
/// FirstCandleUtc/LastCandleUtc là khoảng nến được giao dịch; WarmupCandles nến trước đó chỉ để có đủ cửa sổ.
/// EvaluatedCandles là số nến chiến lược được hỏi, SkippedGapWindows là số nến bỏ qua vì cửa sổ thiếu nến.
/// </summary>
public sealed record BacktestDto(
    long Id,
    DateTime CreatedAtUtc,
    string? Note,
    int StrategyId,
    string Symbol,
    string Interval,
    EmaCrossParameters Parameters,
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
