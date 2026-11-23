using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public interface IPaperFuturesStore
{
    Task<IReadOnlyList<FuturesPosition>> GetOpenPositionsAsync(CancellationToken ct);
    /// <summary>Vị thế theo Id với trạng thái mới nhất trong database (đọc lại cả khi đã được đọc trước đó).</summary>
    Task<FuturesPosition?> GetAsync(long id, CancellationToken ct);
    Task<FuturesPosition?> FindByOpenCommandAsync(Guid commandId, CancellationToken ct);
    Task<IReadOnlyList<FuturesPosition>> GetClosedAsync(ClosedPositionFilter filter, int limit, CancellationToken ct);
    Task<IReadOnlyList<PaperLedgerEntry>> GetLedgerAsync(CancellationToken ct);
    void AddPosition(FuturesPosition position);
    void AddLedger(PaperLedgerEntry entry);

    /// <summary>
    /// Một lần gọi = một transaction (vị thế + bút toán cùng thành công hoặc cùng rollback). Ném
    /// <see cref="Abstractions.ConcurrentUpdateException"/> khi vị thế đã bị bên khác thay đổi sau khi đọc.
    /// </summary>
    Task SaveAsync(CancellationToken ct);
}
