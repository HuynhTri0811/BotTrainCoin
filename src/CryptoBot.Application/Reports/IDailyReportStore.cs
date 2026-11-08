using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Reports;

public interface IDailyReportStore
{
    Task<bool> ExistsAsync(string symbol, DateTime periodEndUtc, CancellationToken ct);

    /// <summary>Tối đa <paramref name="limit"/> báo cáo mới nhất (theo cuối khoảng), mới → cũ.</summary>
    Task<IReadOnlyList<DailyReport>> GetRecentAsync(string? symbol, int limit, CancellationToken ct);

    void Add(DailyReport report);
    Task SaveAsync(CancellationToken ct);
}
