using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Futures;
using CryptoBot.Application.Indicators;
using CryptoBot.Domain.Futures;
using CryptoBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.IntegrationTests;

/// <summary>
/// Hủy quy tắc (request) và kích hoạt quy tắc (worker) chạy trên hai DbContext khác nhau, chung các singleton như
/// trong app thật. Chạy trên PostgreSQL thật.
/// </summary>
public sealed class AutoOrderConcurrencyTests : IAsyncLifetime
{
    private static readonly DateTime T0 = new(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresTestDatabase _database = new();
    private readonly TestClock _clock = new(T0);
    private readonly PaperFuturesLock _paperGate = new();
    private readonly AutoOrderLock _ruleGate = new();
    private readonly AutoOrderOptions _options = new();
    private readonly IndicatorRegistry _registry = new([], NullLogger<IndicatorRegistry>.Instance);
    private readonly PaperFuturesOptions _paperOptions = new()
    {
        StartingBalance = 1000m,
        TakerFeeRate = 0.0005m,
        SlippageRate = 0m,
        Symbols = [new FuturesSymbolOptions { Symbol = "BTCUSDT", QuantityStep = 0.001m, TickSize = 0.1m, MinNotional = 50m, MaintenanceMarginRate = 0.004m }],
    };
    private readonly MarkPriceCache _marks;

    public AutoOrderConcurrencyTests() => _marks = new MarkPriceCache(_clock, _paperOptions);

    public Task InitializeAsync() => _database.CreateAsync();

    public Task DisposeAsync() => _database.DropAsync();

    private AutoOrderService NewService(BotDbContext db, IAutoOrderStore? store = null)
    {
        PaperFuturesStore paperStore = new(db);
        PaperFuturesService paper = new(paperStore, _marks, _paperGate, _paperOptions, _clock,
            NullLogger<PaperFuturesService>.Instance);
        return new AutoOrderService(store ?? new AutoOrderStore(db), paperStore, paper, _ruleGate, _marks,
            new IndicatorService(_registry, new CandleStore(db)), _registry, _paperOptions, _options, _clock,
            NullLogger<AutoOrderService>.Instance);
    }

    private void SetMark(decimal price) => _marks.Set(new MarkPrice("BTCUSDT", price, _clock.UtcNow));

    private async Task<long> CreateRuleAsync()
    {
        SetMark(100m);
        await using BotDbContext db = _database.NewContext();
        CreateRuleResult result = await NewService(db).CreateAsync(new CreateRuleRequest("BTCUSDT",
            PositionSide.Long, TriggerCondition.AtOrBelow, 99m, 200m, 2, 1m, 2m), default);
        return Assert.IsType<AutoOrderRule>(result.Rule).Id;
    }

    [Fact]
    public async Task Successful_cancel_prevents_the_worker_opening_its_cached_rule()
    {
        long ruleId = await CreateRuleAsync();
        SetMark(99m);

        await using BotDbContext workerDb = _database.NewContext();
        PausingAutoOrderStore workerStore = new(new AutoOrderStore(workerDb));
        Task<int> evaluation = NewService(workerDb, workerStore).EvaluateAsync(default);
        // Worker đã đọc danh sách Active và dừng ngay trước khi mở lệnh.
        await workerStore.ActiveRead.Task;

        await using (BotDbContext requestDb = _database.NewContext())
            Assert.True((await NewService(requestDb).CancelAsync(ruleId, default)).Cancelled);

        workerStore.Resume.SetResult();
        Assert.Equal(0, await evaluation);

        await using BotDbContext check = _database.NewContext();
        Assert.Equal(AutoRuleStatus.Cancelled, (await check.AutoOrderRules.SingleAsync()).Status);
        Assert.Empty(await check.FuturesPositions.ToListAsync());
    }

    [Fact]
    public async Task Stale_rule_cannot_overwrite_a_cancelled_status()
    {
        long ruleId = await CreateRuleAsync();
        await using BotDbContext staleDb = _database.NewContext();
        AutoOrderRule stale = await staleDb.AutoOrderRules.SingleAsync(rule => rule.Id == ruleId);

        await using (BotDbContext requestDb = _database.NewContext())
            Assert.True((await NewService(requestDb).CancelAsync(ruleId, default)).Cancelled);

        stale.MarkTriggered(99m, 1, T0);
        await Assert.ThrowsAsync<ConcurrentUpdateException>(() => new AutoOrderStore(staleDb).SaveAsync(default));

        Assert.Equal(AutoRuleStatus.Cancelled, stale.Status);
        await using BotDbContext check = _database.NewContext();
        Assert.Equal(AutoRuleStatus.Cancelled, (await check.AutoOrderRules.SingleAsync()).Status);
    }

    /// <summary>Dừng worker ngay sau khi đọc danh sách quy tắc Active, cho đến khi test cho chạy tiếp.</summary>
    private sealed class PausingAutoOrderStore(IAutoOrderStore inner) : IAutoOrderStore
    {
        public TaskCompletionSource ActiveRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IReadOnlyList<AutoOrderRule>> GetActiveAsync(CancellationToken ct)
        {
            IReadOnlyList<AutoOrderRule> active = await inner.GetActiveAsync(ct);
            ActiveRead.SetResult();
            await Resume.Task;
            return active;
        }

        public Task<IReadOnlyList<AutoOrderRule>> GetWatchingCloseAsync(CancellationToken ct) =>
            inner.GetWatchingCloseAsync(ct);

        public Task<IReadOnlyList<AutoOrderRule>> GetRecentAsync(int limit, CancellationToken ct) =>
            inner.GetRecentAsync(limit, ct);

        public Task<AutoOrderRule?> GetAsync(long id, CancellationToken ct) => inner.GetAsync(id, ct);

        public void Add(AutoOrderRule rule) => inner.Add(rule);

        public Task SaveAsync(CancellationToken ct) => inner.SaveAsync(ct);
    }
}
