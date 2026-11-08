using CryptoBot.Domain.MarketData;

namespace CryptoBot.Api.Endpoints;

/// <summary>Một báo cáo đã lưu; <see cref="Report"/> là nguyên nội dung DailyMoveReport.</summary>
public sealed record DailyReportDto(string Symbol, string Interval, DateTime CreatedAtUtc, DailyMoveReport Report);
