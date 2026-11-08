using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public interface IAutoOrderStore
{
    Task<IReadOnlyList<AutoOrderRule>> GetActiveAsync(CancellationToken ct);

    /// <summary>Quy tắc đã mở lệnh và đang chờ chỉ báo đảo chiều để tự đóng.</summary>
    Task<IReadOnlyList<AutoOrderRule>> GetWatchingCloseAsync(CancellationToken ct);
    Task<IReadOnlyList<AutoOrderRule>> GetRecentAsync(int limit, CancellationToken ct);
    Task<AutoOrderRule?> GetAsync(long id, CancellationToken ct);
    void Add(AutoOrderRule rule);
    Task SaveAsync(CancellationToken ct);
}
