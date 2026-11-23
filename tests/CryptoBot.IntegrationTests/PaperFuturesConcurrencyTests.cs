using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Futures;
using CryptoBot.Domain.Futures;
using CryptoBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.IntegrationTests;

/// <summary>
/// Worker và request đóng tay dùng hai DbContext khác nhau nhưng chung khóa, mark price và cấu hình (singleton),
/// như trong app thật. Chạy trên PostgreSQL thật.
/// </summary>
public sealed class PaperFuturesConcurrencyTests : IAsyncLifetime
{
    private static readonly DateTime T0 = new(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresTestDatabase _database = new();
    private readonly TestClock _clock = new(T0);
    private readonly PaperFuturesLock _gate = new();
    private readonly PaperFuturesOptions _options = new()
    {
        StartingBalance = 1000m,
        TakerFeeRate = 0.0005m,
        SlippageRate = 0m,
        Symbols = [new FuturesSymbolOptions { Symbol = "BTCUSDT", QuantityStep = 0.001m, TickSize = 0.1m, MinNotional = 50m, MaintenanceMarginRate = 0.004m }],
    };
    private readonly MarkPriceCache _marks;

    public PaperFuturesConcurrencyTests() => _marks = new MarkPriceCache(_clock, _options);

    public Task InitializeAsync() => _database.CreateAsync();

    public Task DisposeAsync() => _database.DropAsync();

    private PaperFuturesService NewService(BotDbContext db) =>
        new(new PaperFuturesStore(db), _marks, _gate, _options, _clock, NullLogger<PaperFuturesService>.Instance);

    private void SetMark(decimal price) => _marks.Set(new MarkPrice("BTCUSDT", price, _clock.UtcNow));

    private async Task<long> OpenLongAsync()
    {
        SetMark(100m);
        await using BotDbContext db = _database.NewContext();
        OpenResult result = await NewService(db).OpenAsync(
            new OpenPositionRequest(Guid.NewGuid(), "BTCUSDT", PositionSide.Long, 2, 100m, 99m, null), default);
        return Assert.IsType<FuturesPosition>(result.Position).Id;
    }

    private async Task<List<decimal>> RealizedPnlAsync()
    {
        await using BotDbContext db = _database.NewContext();
        return await db.PaperLedger.Where(entry => entry.Type == LedgerEntryType.RealizedPnl)
            .Select(entry => entry.Amount).ToListAsync();
    }

    [Fact]
    public async Task Worker_does_not_close_a_position_already_closed_by_a_manual_request()
    {
        long positionId = await OpenLongAsync();
        await using BotDbContext workerDb = _database.NewContext();
        PaperFuturesService worker = NewService(workerDb);
        // Chưa chạm SL/TP: không đóng gì, nhưng vị thế Open đã được DbContext của worker tracking.
        Assert.Equal(0, await worker.EvaluateTriggersAsync(default));

        SetMark(110m);
        Guid manualCommand = Guid.NewGuid();
        await using (BotDbContext requestDb = _database.NewContext())
            Assert.True((await NewService(requestDb).CloseAsync(positionId, manualCommand, default)).Ok);

        CloseResult second = await worker.CloseAsync(positionId, Guid.NewGuid(), CloseReason.Signal, default);

        Assert.False(second.Ok);
        Assert.Equal(PositionStatus.Closed, second.Position!.Status);
        Assert.Equal(CloseReason.Manual, second.Position.CloseReason);
        Assert.Equal([20m], await RealizedPnlAsync());

        // Retry đúng lệnh đóng cũ, kể cả từ DbContext cũ của worker, vẫn nhận lại kết quả cũ.
        CloseResult retry = await worker.CloseAsync(positionId, manualCommand, default);
        Assert.True(retry.Ok);
        Assert.Equal(manualCommand, retry.Position!.CloseCommandId);
        Assert.Equal([20m], await RealizedPnlAsync());
    }

    [Fact]
    public async Task Persistence_rejects_a_second_close_from_a_stale_read()
    {
        // Hai tiến trình (không chung khóa) cùng đọc vị thế Open rồi cùng đóng: chỉ lần lưu đầu thành công.
        long positionId = await OpenLongAsync();
        await using BotDbContext firstDb = _database.NewContext();
        await using BotDbContext secondDb = _database.NewContext();
        PaperFuturesStore first = new(firstDb);
        PaperFuturesStore second = new(secondDb);
        FuturesPosition firstRead = (await first.GetAsync(positionId, default))!;
        FuturesPosition secondRead = (await second.GetAsync(positionId, default))!;

        firstRead.Close(CloseReason.Manual, 110m, 0.1m, Guid.NewGuid(), T0);
        first.AddLedger(new PaperLedgerEntry(20m, LedgerEntryType.RealizedPnl, firstRead, "Đóng", T0));
        await first.SaveAsync(default);

        secondRead.Close(CloseReason.Signal, 110m, 0.1m, Guid.NewGuid(), T0);
        second.AddLedger(new PaperLedgerEntry(20m, LedgerEntryType.RealizedPnl, secondRead, "Đóng", T0));
        await Assert.ThrowsAsync<ConcurrentUpdateException>(() => second.SaveAsync(default));

        // Thay đổi bị bỏ và vị thế được đọc lại theo trạng thái thật.
        Assert.Equal(CloseReason.Manual, secondRead.CloseReason);
        Assert.Equal([20m], await RealizedPnlAsync());
    }
}
