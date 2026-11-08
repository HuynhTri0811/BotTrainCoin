using CryptoBot.Application.Reports;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Tests;

internal sealed class InMemoryDailyReportStore : IDailyReportStore
{
    public readonly List<DailyReport> Reports = [];
    private readonly List<DailyReport> _pending = [];

    public Task<bool> ExistsAsync(string symbol, DateTime periodEndUtc, CancellationToken ct) =>
        Task.FromResult(Reports.Any(report => report.Symbol == symbol && report.PeriodEndUtc == periodEndUtc));

    public Task<IReadOnlyList<DailyReport>> GetRecentAsync(string? symbol, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DailyReport>>(Reports
            .Where(report => symbol is null || report.Symbol == symbol)
            .OrderByDescending(report => report.PeriodEndUtc).Take(limit).ToList());

    public void Add(DailyReport report) => _pending.Add(report);

    public Task SaveAsync(CancellationToken ct)
    {
        Reports.AddRange(_pending);
        _pending.Clear();
        return Task.CompletedTask;
    }
}
