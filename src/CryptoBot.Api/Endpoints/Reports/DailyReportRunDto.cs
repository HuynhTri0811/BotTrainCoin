namespace CryptoBot.Api.Endpoints;

public sealed record DailyReportRunDto(DateTime PeriodEndUtc, int Created, IReadOnlyList<string> Errors);
