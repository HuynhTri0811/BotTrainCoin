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
    }
}

public sealed record LiveSampleDto(DateTime TimeUtc, decimal? SpotPrice, decimal? MarkPrice, int? SpotLatencyMs,
    int? MarkLatencyMs, string? Error);

public sealed record LiveSymbolDto(string Symbol, IReadOnlyList<LiveSampleDto> Samples);

public sealed record LivePricesDto(int IntervalSeconds, DateTime ServerTimeUtc, IReadOnlyList<LiveSymbolDto> Symbols);
