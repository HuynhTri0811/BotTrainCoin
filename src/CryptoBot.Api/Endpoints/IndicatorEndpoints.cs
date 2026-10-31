using CryptoBot.Application.Indicators;
using CryptoBot.Domain.Indicators;

namespace CryptoBot.Api.Endpoints;

/// <summary>Chỉ báo tự viết: danh sách đã cấu hình và dữ liệu để vẽ chart.</summary>
public static class IndicatorEndpoints
{
    public static void MapIndicatorEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/indicators");

        api.MapGet("", (IndicatorRegistry registry) => new IndicatorListDto(
            registry.All.Select(ToDto).ToList(),
            registry.Errors.Select(error => new IndicatorErrorDto(error.Name, error.Error)).ToList(),
            IndicatorRegistry.AvailableTypes.Keys.Order().ToList()));

        api.MapGet("/{name}/chart", async (string name, int? limit, IndicatorService svc, CancellationToken ct) =>
        {
            var chart = await svc.GetChartAsync(name, limit ?? 300, ct);
            if (chart is null)
                return Results.NotFound(new { error = $"Không có chỉ báo '{name}'." });

            return Results.Ok(new IndicatorChartDto(ToDto(chart.Indicator),
                chart.Points.Select(point => new IndicatorPointDto(ToUnixSeconds(point.Candle.OpenTimeUtc),
                    point.Result.Values,
                    point.Result.Signal.ToString())).ToList(),
                chart.Points.Where(point => point.Result.Signal != IndicatorSignal.None)
                    .Select(point => new IndicatorMarkerDto(ToUnixSeconds(point.Candle.OpenTimeUtc),
                        point.Result.Signal.ToString(),
                        point.Result.Reason)).ToList(),
                chart.Points.Count > 0 ? chart.Points[^1].Result.Reason : null));
        });
    }

    private static long ToUnixSeconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();

    private static IndicatorDto ToDto(ConfiguredIndicator indicator) => new(indicator.Name, indicator.Type,
        indicator.Description, indicator.Key.Symbol,
        indicator.Key.Interval.Code, indicator.Indicator.RequiredCandles,
        indicator.Indicator.Lines.Select(line => new IndicatorLineDto(line.Key, line.Title, line.Color, line.Overlay,
            line.Levels ?? [])).ToList());
}

public sealed record IndicatorLineDto(string Key, string Title, string Color, bool Overlay, IReadOnlyList<decimal> Levels);

public sealed record IndicatorDto(string Name, string Type, string Description, string Symbol, string Interval,
    int RequiredCandles, IReadOnlyList<IndicatorLineDto> Lines);

public sealed record IndicatorErrorDto(string Name, string Error);

public sealed record IndicatorListDto(IReadOnlyList<IndicatorDto> Indicators, IReadOnlyList<IndicatorErrorDto> Errors,
    IReadOnlyList<string> AvailableTypes);

/// <summary>Time là Unix seconds (open time của nến đã đóng). Values: khóa = <see cref="IndicatorLineDto.Key"/>.</summary>
public sealed record IndicatorPointDto(long Time, IReadOnlyDictionary<string, decimal?> Values, string Signal);

public sealed record IndicatorMarkerDto(long Time, string Signal, string Reason);

public sealed record IndicatorChartDto(IndicatorDto Indicator, IReadOnlyList<IndicatorPointDto> Points,
    IReadOnlyList<IndicatorMarkerDto> Markers, string? LatestReason);
