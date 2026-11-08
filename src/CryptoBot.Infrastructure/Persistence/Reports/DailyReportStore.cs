using CryptoBot.Application.Reports;
using CryptoBot.Domain.MarketData;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.Infrastructure.Persistence;

public sealed class DailyReportStore(BotDbContext db) : IDailyReportStore
{
    public Task<bool> ExistsAsync(string symbol, DateTime periodEndUtc, CancellationToken ct) =>
        db.DailyReports.AnyAsync(report => report.Symbol == symbol && report.PeriodEndUtc == periodEndUtc, ct);

    public async Task<IReadOnlyList<DailyReport>> GetRecentAsync(string? symbol, int limit, CancellationToken ct) =>
        await db.DailyReports.AsNoTracking()
            .Where(report => symbol == null || report.Symbol == symbol)
            .OrderByDescending(report => report.PeriodEndUtc).ThenBy(report => report.Symbol)
            .Take(limit).ToListAsync(ct);

    public void Add(DailyReport report) => db.DailyReports.Add(report);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
