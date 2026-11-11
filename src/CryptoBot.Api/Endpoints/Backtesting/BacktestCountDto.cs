namespace CryptoBot.Api.Endpoints;

/// <summary>Tổng số lần backtest đã lưu (theo bộ lọc chiến lược nếu có).</summary>
public sealed record BacktestCountDto(int Total);
