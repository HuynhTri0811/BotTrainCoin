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
            var symbols = options.Symbols.Select(symbol => new LiveSymbolDto(symbol,
                history.Get(symbol, take).Select(sample => new LiveSampleDto(sample.TimeUtc, sample.SpotPrice,
                    sample.MarkPrice,
                    sample.SpotLatencyMs, sample.MarkLatencyMs, sample.Error)).ToList())).ToList();
            return new LivePricesDto((int)options.PollInterval.TotalSeconds, clock.UtcNow, symbols);
        });

        // days: số ngày phía trước (mặc định 1 = 24 giờ).
        app.MapGet("/api/live/daily-range", async (string symbol, int? days, LivePriceOptions options,
            DailyRangeService ranges, SimilarDaysService similar, CancellationToken ct) =>
        {
            if (!options.Symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = $"Symbol không có trong LivePrices: {symbol}" });
            var horizon = days ?? 1;
            if (horizon < 1 || horizon > DailyRangeService.MaxHorizonDays)
                return Results.BadRequest(new { error = $"days phải từ 1 đến {DailyRangeService.MaxHorizonDays}." });

            var range = await ranges.GetAsync(symbol.ToUpperInvariant(), horizon, ct);
            if (range is null)
                return Results.NotFound(new { error = "Chưa đủ dữ liệu nến ngày." });

            // Ngày tương tự chỉ cho 24 giờ: chạy thử cho thấy 7/30 ngày không có lợi thế so với đoán mặc định.
            var result = horizon == 1 ? await similar.GetAsync(symbol.ToUpperInvariant(), horizon, ct) : null;
            var similarDto = result is null ? null : new SimilarDaysDto(result.HistorySymbol, result.Exchange,
                result.AsOfDayUtc,
                result.AnchorPrice, result.TargetCloseUtc, result.Forecast.Neighbors, result.Forecast.UpPercent,
                    result.Forecast.MedianPercent,
                result.Forecast.Low68Percent, result.Forecast.High68Percent, result.Low68, result.High68,
                    result.Forecast.Lean.ToString(),
                result.Backtest.Days, result.Backtest.HitRatePercent, result.Backtest.StrongDays,
                    result.Backtest.StrongHitRatePercent);
            return Results.Ok(new DailyRangeDto(symbol.ToUpperInvariant(), range.HorizonDays, range.Days, range.Price,
                range.TypicalMovePercent, range.Low68, range.High68, range.Low95, range.High95,
                    range.AverageMovePercent,
                range.LargestRisePercent, range.LargestDropPercent, range.UpDaysPercent, range.Samples, similarDto));
        });
    }
}
