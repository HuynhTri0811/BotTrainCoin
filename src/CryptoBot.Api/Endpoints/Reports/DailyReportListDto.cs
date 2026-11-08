namespace CryptoBot.Api.Endpoints;

public sealed record DailyReportListDto(IReadOnlyList<string> Symbols, string RunAt, string TimeZone,
    DateTime NextRunUtc, IReadOnlyList<DailyReportDto> Reports);
