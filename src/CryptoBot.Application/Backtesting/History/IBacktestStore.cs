using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Application.Backtesting;

public interface IBacktestStore
{
    /// <summary>
    /// Tối đa <paramref name="limit"/> lần chạy (lọc theo instance nếu có), mới → cũ, bỏ qua <paramref name="offset"/>
    /// lần chạy đầu; không đọc nội dung.
    /// </summary>
    Task<IReadOnlyList<BacktestSummary>> ListAsync(int? strategyInstanceId, int limit, int offset,
        CancellationToken ct);

    /// <summary>Tổng số lần chạy (lọc theo instance nếu có).</summary>
    Task<int> CountAsync(int? strategyInstanceId, CancellationToken ct);

    /// <summary>Lần chạy kèm nội dung; null nếu không có.</summary>
    Task<SavedBacktest?> GetAsync(long id, CancellationToken ct);

    /// <summary>Ghi đè ghi chú mà không đọc nội dung; false nếu không có lần chạy này.</summary>
    Task<bool> SetNoteAsync(long id, string? note, CancellationToken ct);

    /// <summary>Xóa mà không đọc nội dung; false nếu không có lần chạy này.</summary>
    Task<bool> DeleteAsync(long id, CancellationToken ct);

    void Add(SavedBacktest backtest);
    Task SaveAsync(CancellationToken ct);
}
