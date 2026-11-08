using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Reports;

/// <summary>Báo cáo đã lưu kèm nội dung đã đọc lại từ JSON.</summary>
public sealed record SavedDailyReport(string Symbol, string Interval, DateTime CreatedAtUtc, DailyMoveReport Report);
