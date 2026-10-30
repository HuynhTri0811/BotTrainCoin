using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Reports;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/reports/daily");

        // Báo cáo biến động đã lập (DailyReportWorker, mỗi ngày lúc RunAt), mới → cũ.
        api.MapGet("", async (string? symbol, int? limit, DailyReportService svc, DailyReportOptions options,
            IClock clock, CancellationToken ct) =>
        {
            var picked = symbol is null
                ? null
                : options.Symbols.FirstOrDefault(s => s.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (symbol is not null && picked is null)
                return Results.BadRequest(new { error = $"Symbol không có trong DailyReports: {symbol}" });

            var reports = await svc.GetRecentAsync(picked, Math.Clamp(limit ?? 14, 1, 100), ct);
            return Results.Ok(new DailyReportListDto(options.Symbols, options.RunAt.ToString(@"hh\:mm"),
                options.TimeZone, svc.NextRunUtc(clock.UtcNow),
                reports.Select(r => new DailyReportDto(r.Symbol, r.Interval, r.CreatedAtUtc, r.Report)).ToList()));
        });

        // Lập ngay báo cáo còn thiếu của giờ lập gần nhất (không tạo trùng nếu đã có).
        api.MapPost("/run", async (DailyReportService svc, CancellationToken ct) =>
        {
            var run = await svc.RunDueAsync(ct);
            return Results.Ok(new DailyReportRunDto(run.PeriodEndUtc, run.Created, run.Errors));
        });
    }
}

/// <summary>Một báo cáo đã lưu; <see cref="Report"/> là nguyên nội dung DailyMoveReport.</summary>
public sealed record DailyReportDto(string Symbol, string Interval, DateTime CreatedAtUtc, DailyMoveReport Report);

public sealed record DailyReportListDto(IReadOnlyList<string> Symbols, string RunAt, string TimeZone,
    DateTime NextRunUtc, IReadOnlyList<DailyReportDto> Reports);

public sealed record DailyReportRunDto(DateTime PeriodEndUtc, int Created, IReadOnlyList<string> Errors);
