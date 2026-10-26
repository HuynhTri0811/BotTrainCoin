using CryptoBot.Application.Abstractions;
using CryptoBot.Application.MarketData;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Api.Endpoints;

public static class HistoryEndpoints
{
    /// <summary>Đủ cho hơn 50 năm nến ngày.</summary>
    private const int MaxDailyCandles = 20_000;

    public static void MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        // Chênh lệch mở → đóng cửa từng tháng trên nến ngày lịch sử (DailyHistoryWorker, mặc định Bitfinex từ 2016).
        app.MapGet("/api/history/monthly", async (string? symbol, DailyHistoryOptions options,
            IDailyHistoryClient client, ICandleStore candles, CancellationToken ct) =>
        {
            if (options.Symbols.Count == 0)
                return Results.NotFound(new { error = "Chưa cấu hình DailyHistory:Symbols." });
            var picked = symbol is null
                ? options.Symbols[0]
                : options.Symbols.FirstOrDefault(s => s.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (picked is null)
                return Results.BadRequest(new { error = $"Symbol không có trong DailyHistory: {symbol}" });

            var daily = await candles.GetLatestAsync(new CandleKey(client.Exchange, picked, CandleInterval.D1), null,
                MaxDailyCandles, closedOnly: false, ct);
            if (daily.Count == 0)
                return Results.NotFound(new { error = $"Chưa có nến ngày {picked}. Worker đang tải lần đầu." });

            var report = MonthlyChangeReport.Build(daily);
            return Results.Ok(new MonthlyHistoryDto(picked, client.Exchange, options.Symbols,
                daily[0].OpenTimeUtc, daily[^1].OpenTimeUtc,
                report.Months.Select(m => new MonthChangeDto(m.Year, m.Month, m.Open, m.Close, m.ChangePercent,
                    m.Days, m.Complete)).ToList(),
                report.Years.Select(y => new YearChangeDto(y.Year, y.Open, y.Close, y.ChangePercent, y.Complete))
                    .ToList(),
                report.MonthStats.Select(s => new MonthStatsDto(s.Month, s.Years, s.AverageChangePercent,
                    s.BestChangePercent, s.WorstChangePercent, s.UpCount, s.UpPercent)).ToList()));
        });
    }
}

public sealed record MonthChangeDto(int Year, int Month, decimal Open, decimal Close, decimal ChangePercent, int Days,
    bool Complete);

public sealed record YearChangeDto(int Year, decimal Open, decimal Close, decimal ChangePercent, bool Complete);

public sealed record MonthStatsDto(int Month, int Years, decimal AverageChangePercent, decimal BestChangePercent,
    decimal WorstChangePercent, int UpCount, decimal UpPercent);

/// <summary>Báo cáo theo tháng (xem MonthlyChangeReport). Số chỉ để hiển thị.</summary>
public sealed record MonthlyHistoryDto(string Symbol, string Exchange, IReadOnlyList<string> Symbols,
    DateTime FirstDayUtc, DateTime LastDayUtc, IReadOnlyList<MonthChangeDto> Months, IReadOnlyList<YearChangeDto> Years,
    IReadOnlyList<MonthStatsDto> MonthStats);
