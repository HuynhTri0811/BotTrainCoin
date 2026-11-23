using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Reports;

public interface IDailyReportStore
{
    /// <summary>Báo cáo của (symbol, giờ lập) để cập nhật; null khi chưa có.</summary>
    Task<DailyReport?> FindAsync(string symbol, DateTime periodEndUtc, CancellationToken ct);

    /// <summary>Tối đa <paramref name="limit"/> báo cáo mới nhất (theo cuối khoảng), mới → cũ.</summary>
    Task<IReadOnlyList<DailyReport>> GetRecentAsync(string? symbol, int limit, CancellationToken ct);

    void Add(DailyReport report);
    Task SaveAsync(CancellationToken ct);
}
