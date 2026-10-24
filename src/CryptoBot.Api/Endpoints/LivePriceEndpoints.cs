using CryptoBot.Application.Abstractions;
using CryptoBot.Application.LivePrices;

namespace CryptoBot.Api.Endpoints;

public static class LivePriceEndpoints
{
    public static void MapLivePriceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/live/prices", (int? limit, LivePriceHistory history, LivePriceOptions options, IClock clock) =>
        {
            var take = Math.Clamp(limit ?? 60, 1, options.HistorySize);
            var symbols = options.Symbols.Select(s => new LiveSymbolDto(s,
                history.Get(s, take).Select(x => new LiveSampleDto(x.TimeUtc, x.SpotPrice, x.MarkPrice,
                    x.SpotLatencyMs, x.MarkLatencyMs, x.Error)).ToList())).ToList();
            return new LivePricesDto((int)options.PollInterval.TotalSeconds, clock.UtcNow, symbols);
        });

        app.MapGet("/api/live/daily-range", async (string symbol, LivePriceOptions options, DailyRangeService ranges,
            CancellationToken ct) =>
        {
            if (!options.Symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = $"Symbol không có trong LivePrices: {symbol}" });

            var r = await ranges.GetAsync(symbol.ToUpperInvariant(), ct);
            return r is null
                ? Results.NotFound(new { error = "Chưa đủ dữ liệu nến ngày." })
                : Results.Ok(new DailyRangeDto(symbol.ToUpperInvariant(), r.Days, r.Price, r.TypicalMovePercent,
                    r.Low68, r.High68, r.Low95, r.High95, r.AverageMovePercent, r.LargestRisePercent,
                    r.LargestDropPercent, r.UpDaysPercent));
        });
    }
}

public sealed record LiveSampleDto(DateTime TimeUtc, decimal? SpotPrice, decimal? MarkPrice, int? SpotLatencyMs,
    int? MarkLatencyMs, string? Error);

public sealed record LiveSymbolDto(string Symbol, IReadOnlyList<LiveSampleDto> Samples);

public sealed record LivePricesDto(int IntervalSeconds, DateTime ServerTimeUtc, IReadOnlyList<LiveSymbolDto> Symbols);

/// <summary>Biên độ dự kiến 24 giờ (xem DailyMoveRange). Số chỉ để hiển thị.</summary>
public sealed record DailyRangeDto(string Symbol, int Days, decimal Price, decimal TypicalMovePercent, decimal Low68,
    decimal High68, decimal Low95, decimal High95, decimal AverageMovePercent, decimal LargestRisePercent,
    decimal LargestDropPercent, decimal UpDaysPercent);
