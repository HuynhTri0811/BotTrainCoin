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

        // days: số ngày phía trước (mặc định 1 = 24 giờ).
        app.MapGet("/api/live/daily-range", async (string symbol, int? days, LivePriceOptions options,
            DailyRangeService ranges, SimilarDaysService similar, CancellationToken ct) =>
        {
            if (!options.Symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = $"Symbol không có trong LivePrices: {symbol}" });
            var horizon = days ?? 1;
            if (horizon < 1 || horizon > DailyRangeService.MaxHorizonDays)
                return Results.BadRequest(new { error = $"days phải từ 1 đến {DailyRangeService.MaxHorizonDays}." });

            var r = await ranges.GetAsync(symbol.ToUpperInvariant(), horizon, ct);
            if (r is null)
                return Results.NotFound(new { error = "Chưa đủ dữ liệu nến ngày." });

            // Ngày tương tự chỉ cho 24 giờ: chạy thử cho thấy 7/30 ngày không có lợi thế so với đoán mặc định.
            var s = horizon == 1 ? await similar.GetAsync(symbol.ToUpperInvariant(), horizon, ct) : null;
            var similarDto = s is null ? null : new SimilarDaysDto(s.HistorySymbol, s.Exchange, s.AsOfDayUtc,
                s.AnchorPrice, s.TargetCloseUtc, s.Forecast.Neighbors, s.Forecast.UpPercent, s.Forecast.MedianPercent,
                s.Forecast.Low68Percent, s.Forecast.High68Percent, s.Low68, s.High68, s.Forecast.Lean.ToString(),
                s.Backtest.Days, s.Backtest.HitRatePercent, s.Backtest.StrongDays, s.Backtest.StrongHitRatePercent);
            return Results.Ok(new DailyRangeDto(symbol.ToUpperInvariant(), r.HorizonDays, r.Days, r.Price,
                r.TypicalMovePercent, r.Low68, r.High68, r.Low95, r.High95, r.AverageMovePercent,
                r.LargestRisePercent, r.LargestDropPercent, r.UpDaysPercent, r.Samples, similarDto));
        });
    }
}

public sealed record LiveSampleDto(DateTime TimeUtc, decimal? SpotPrice, decimal? MarkPrice, int? SpotLatencyMs,
    int? MarkLatencyMs, string? Error);

public sealed record LiveSymbolDto(string Symbol, IReadOnlyList<LiveSampleDto> Samples);

public sealed record LivePricesDto(int IntervalSeconds, DateTime ServerTimeUtc, IReadOnlyList<LiveSymbolDto> Symbols);

/// <summary>Biên độ dự kiến sau HorizonDays ngày (xem DailyMoveRange). Số chỉ để hiển thị.</summary>
public sealed record DailyRangeDto(string Symbol, int HorizonDays, int Days, decimal Price,
    decimal TypicalMovePercent, decimal Low68, decimal High68, decimal Low95, decimal High95,
    decimal AverageMovePercent, decimal LargestRisePercent, decimal LargestDropPercent, decimal UpDaysPercent,
    int Samples, SimilarDaysDto? SimilarDays);

/// <summary>
/// Dự báo "ngày tương tự" (SimilarDaysForecast) cho giá đóng cửa lúc TargetCloseUtc, kèm tỷ lệ đúng khi chạy thử.
/// Low68/High68 là giá: AnchorPrice (giá đóng hôm qua) × (1 + % của các ngày tương tự). Lean: Up / Down / Neutral.
/// </summary>
public sealed record SimilarDaysDto(string HistorySymbol, string Exchange, DateTime AsOfDayUtc, decimal AnchorPrice,
    DateTime TargetCloseUtc, int Neighbors, decimal UpPercent, decimal MedianPercent, decimal Low68Percent,
    decimal High68Percent, decimal Low68, decimal High68, string Lean, int TestedDays, decimal HitRatePercent,
    int StrongDays, decimal StrongHitRatePercent);
