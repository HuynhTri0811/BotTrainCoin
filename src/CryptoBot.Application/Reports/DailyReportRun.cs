namespace CryptoBot.Application.Reports;

/// <summary>Kết quả một lượt lập báo cáo: số báo cáo mới và lỗi theo symbol (sàn lỗi, chưa có nến…).</summary>
public sealed record DailyReportRun(DateTime PeriodEndUtc, int Created, IReadOnlyList<string> Errors);
