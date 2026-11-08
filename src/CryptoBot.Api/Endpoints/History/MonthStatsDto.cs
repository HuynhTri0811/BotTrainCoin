namespace CryptoBot.Api.Endpoints;

public sealed record MonthStatsDto(int Month, int Years, decimal AverageChangePercent, decimal BestChangePercent,
    decimal WorstChangePercent, int UpCount, decimal UpPercent);
