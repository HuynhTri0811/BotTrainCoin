using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Application.Backtesting;

public interface IBacktestStore
{
    /// <summary>Tối đa <paramref name="limit"/> lần chạy mới nhất (lọc theo instance nếu có), mới → cũ, không đọc nội dung.</summary>
    Task<IReadOnlyList<BacktestSummary>> ListAsync(int? strategyInstanceId, int limit, CancellationToken ct);

    /// <summary>Lần chạy kèm nội dung; null nếu không có.</summary>
    Task<SavedBacktest?> GetAsync(long id, CancellationToken ct);

    /// <summary>Ghi đè ghi chú mà không đọc nội dung; false nếu không có lần chạy này.</summary>
    Task<bool> SetNoteAsync(long id, string? note, CancellationToken ct);

    /// <summary>Xóa mà không đọc nội dung; false nếu không có lần chạy này.</summary>
    Task<bool> DeleteAsync(long id, CancellationToken ct);

    void Add(SavedBacktest backtest);
    Task SaveAsync(CancellationToken ct);
}
