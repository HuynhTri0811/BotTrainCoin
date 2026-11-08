using CryptoBot.Application.Futures;
using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Tests;

internal sealed class InMemoryPaperFuturesStore : IPaperFuturesStore
{
    public readonly List<FuturesPosition> Positions = [];
    public readonly List<PaperLedgerEntry> Ledger = [];
    private long _nextId = 1;

    public Task<IReadOnlyList<FuturesPosition>> GetOpenPositionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FuturesPosition>>(Positions.Where(position =>
            position.Status == PositionStatus.Open).ToList());

    public Task<FuturesPosition?> GetAsync(long id, CancellationToken ct) =>
        Task.FromResult(Positions.FirstOrDefault(position => position.Id == id));

    public Task<FuturesPosition?> FindByOpenCommandAsync(Guid commandId, CancellationToken ct) =>
        Task.FromResult(Positions.FirstOrDefault(position => position.OpenCommandId == commandId));

    public Task<IReadOnlyList<FuturesPosition>> GetClosedAsync(ClosedPositionFilter filter, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FuturesPosition>>(Positions.Where(position =>
            position.Status != PositionStatus.Open).ToList());

    public Task<IReadOnlyList<PaperLedgerEntry>> GetLedgerAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PaperLedgerEntry>>(Ledger.ToList());

    public void AddPosition(FuturesPosition position)
    {
        typeof(FuturesPosition).GetProperty(nameof(FuturesPosition.Id))!.SetValue(position, _nextId++);
        Positions.Add(position);
    }

    public void AddLedger(PaperLedgerEntry entry) => Ledger.Add(entry);

    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
}
