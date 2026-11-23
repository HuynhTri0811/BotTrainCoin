using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Reports;

/// <summary>Báo cáo đã lưu kèm nội dung đã đọc lại từ JSON.</summary>
/// <param name="IsComplete">false khi sàn mới trả một phần nến của khoảng; báo cáo sẽ được lập lại khi đủ.</param>
public sealed record SavedDailyReport(string Symbol, string Interval, DateTime CreatedAtUtc, DailyMoveReport Report,
    bool IsComplete);
