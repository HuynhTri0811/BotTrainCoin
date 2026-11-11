namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Time là Unix seconds (định dạng của Lightweight Charts). Equity null với cột thuộc phần warm-up hoặc chart chi
/// tiết lệnh. EmaFast/EmaSlow tại nến cuối của cột; null khi chưa đủ nến hoặc lần chạy lưu trước khi có EMA.
/// </summary>
public sealed record BacktestBarDto(long Time, decimal Open, decimal High, decimal Low, decimal Close,
    decimal? Equity, decimal? EmaFast, decimal? EmaSlow);
