namespace CryptoBot.Application.Backtesting;

/// <summary>Một cột trên chart backtest: gộp <see cref="BacktestChart.CandlesPerBar"/> nến liên tiếp.</summary>
/// <param name="OpenTimeUtc">Giờ mở của nến đầu trong cột.</param>
/// <param name="Equity">Tổng vốn ở nến cuối của cột; null nếu nến đó thuộc phần warm-up.</param>
/// <param name="EmaFast">
/// EMA nhanh tại nến cuối của cột, tính đúng quy tắc cửa sổ của bot; null khi chưa đủ nến hoặc với lần chạy lưu
/// trước khi chart có EMA.
/// </param>
/// <param name="EmaSlow">EMA chậm, cùng quy ước với <paramref name="EmaFast"/>.</param>
public sealed record BacktestChartBar(
    DateTime OpenTimeUtc,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal? Equity,
    decimal? EmaFast = null,
    decimal? EmaSlow = null);
