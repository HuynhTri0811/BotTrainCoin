namespace CryptoBot.Api.Endpoints;

public sealed record LiveSampleDto(DateTime TimeUtc, decimal? SpotPrice, decimal? MarkPrice, int? SpotLatencyMs,
    int? MarkLatencyMs, string? Error);
