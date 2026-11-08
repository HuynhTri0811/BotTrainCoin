namespace CryptoBot.Api.Endpoints;

/// <summary>Báo cáo theo tháng (xem MonthlyChangeReport). Số chỉ để hiển thị.</summary>
public sealed record MonthlyHistoryDto(string Symbol, string Exchange, IReadOnlyList<string> Symbols,
    DateTime FirstDayUtc, DateTime LastDayUtc, IReadOnlyList<MonthChangeDto> Months, IReadOnlyList<YearChangeDto> Years,
    IReadOnlyList<MonthStatsDto> MonthStats, IReadOnlyList<MonthEventDto> Events);
