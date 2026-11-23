using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public interface IAutoOrderStore
{
    Task<IReadOnlyList<AutoOrderRule>> GetActiveAsync(CancellationToken ct);

    /// <summary>Quy tắc đã mở lệnh và đang chờ chỉ báo đảo chiều để tự đóng.</summary>
    Task<IReadOnlyList<AutoOrderRule>> GetWatchingCloseAsync(CancellationToken ct);
    Task<IReadOnlyList<AutoOrderRule>> GetRecentAsync(int limit, CancellationToken ct);
    /// <summary>Quy tắc theo Id với trạng thái mới nhất trong database (đọc lại cả khi đã được đọc trước đó).</summary>
    Task<AutoOrderRule?> GetAsync(long id, CancellationToken ct);
    void Add(AutoOrderRule rule);
    /// <summary>Ném <see cref="Abstractions.ConcurrentUpdateException"/> khi quy tắc đã bị bên khác thay đổi sau khi đọc.</summary>
    Task SaveAsync(CancellationToken ct);
}
