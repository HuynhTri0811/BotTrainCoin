using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Application.Backtesting;

public interface IBacktestStore
{
    /// <summary>Tối đa <paramref name="limit"/> lần chạy mới nhất (lọc theo instance nếu có), mới → cũ, không đọc nội dung.</summary>
    Task<IReadOnlyList<BacktestSummary>> ListAsync(int? strategyInstanceId, int limit, CancellationToken ct);

    /// <summary>Lần chạy kèm nội dung, được theo dõi để sửa ghi chú; null nếu không có.</summary>
    Task<SavedBacktest?> GetAsync(long id, CancellationToken ct);

    void Add(SavedBacktest backtest);
    void Remove(SavedBacktest backtest);
    Task SaveAsync(CancellationToken ct);
}
