using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Backtesting;

/// <summary>Yêu cầu chạy backtest cho một strategy instance.</summary>
/// <param name="StrategyId">Instance cho biết sàn/cặp/khung nến và tham số mặc định.</param>
/// <param name="FromUtc">Nến đầu tiên được giao dịch (null = từ nến sớm nhất trong DB).</param>
/// <param name="ToUtc">Nến cuối (null = đến nến đóng mới nhất).</param>
/// <param name="Parameters">Tham số EMA thay cho tham số của instance; null = dùng của instance.</param>
/// <param name="Settings">Vốn, khối lượng, TP/SL, phí.</param>
public sealed record BacktestRequest(
    int StrategyId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    EmaCrossParameters? Parameters,
    BacktestSettings Settings);
