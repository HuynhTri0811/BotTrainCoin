namespace CryptoBot.Application.Backtesting;

/// <summary>Khoảng nến đã đóng đang có trong DB cho cặp/khung nến của một strategy instance.</summary>
/// <param name="FirstOpenTimeUtc">Nến đóng sớm nhất; null khi chưa có nến.</param>
/// <param name="LastOpenTimeUtc">Nến đóng mới nhất; null khi chưa có nến.</param>
public sealed record BacktestCoverage(DateTime? FirstOpenTimeUtc, DateTime? LastOpenTimeUtc);
