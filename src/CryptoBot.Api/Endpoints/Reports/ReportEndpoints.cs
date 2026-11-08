using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Reports;

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
                : options.Symbols.FirstOrDefault(candidate => candidate.Equals(symbol,
                    StringComparison.OrdinalIgnoreCase));
            if (symbol is not null && picked is null)
                return Results.BadRequest(new { error = $"Symbol không có trong DailyReports: {symbol}" });

            var reports = await svc.GetRecentAsync(picked, Math.Clamp(limit ?? 14, 1, 100), ct);
            return Results.Ok(new DailyReportListDto(options.Symbols, options.RunAt.ToString(@"hh\:mm"),
                options.TimeZone, svc.NextRunUtc(clock.UtcNow),
                reports.Select(saved => new DailyReportDto(saved.Symbol, saved.Interval, saved.CreatedAtUtc,
                    saved.Report)).ToList()));
        });

        // Lập ngay báo cáo còn thiếu của giờ lập gần nhất (không tạo trùng nếu đã có).
        api.MapPost("/run", async (DailyReportService svc, CancellationToken ct) =>
        {
            var run = await svc.RunDueAsync(ct);
            return Results.Ok(new DailyReportRunDto(run.PeriodEndUtc, run.Created, run.Errors));
        });
    }
}
