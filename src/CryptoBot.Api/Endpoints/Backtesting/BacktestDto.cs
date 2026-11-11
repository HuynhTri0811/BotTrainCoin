using CryptoBot.Domain.Strategies;

namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Kết quả backtest. FirstCandleUtc/LastCandleUtc là khoảng nến được giao dịch; WarmupCandles nến trước đó chỉ để có
/// đủ cửa sổ. EvaluatedCandles là số nến chiến lược được hỏi, SkippedGapWindows là số nến bỏ qua vì cửa sổ thiếu nến.
/// </summary>
public sealed record BacktestDto(
    int StrategyId,
    string Symbol,
    string Interval,
    EmaCrossParameters Parameters,
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
