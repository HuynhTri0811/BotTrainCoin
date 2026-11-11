namespace CryptoBot.Application.Backtesting;

/// <summary>Một cột trên chart backtest: gộp <see cref="BacktestChart.CandlesPerBar"/> nến liên tiếp.</summary>
/// <param name="OpenTimeUtc">Giờ mở của nến đầu trong cột.</param>
/// <param name="Equity">Tổng vốn ở nến cuối của cột; null nếu nến đó thuộc phần warm-up.</param>
public sealed record BacktestChartBar(
    DateTime OpenTimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal? Equity);
