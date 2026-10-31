using CryptoBot.Application.Futures;
using CryptoBot.Domain.Futures;
using Microsoft.Extensions.Logging.Abstractions;

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

public class PaperFuturesServiceTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeClock _clock = new(T0);
    private readonly InMemoryPaperFuturesStore _store = new();
    private readonly PaperFuturesOptions _options = new()
    {
        StartingBalance = 1000m,
        TakerFeeRate = 0.0005m,
        SlippageRate = 0m,
        MaxLeverage = 10,
        Symbols = [new FuturesSymbolOptions { Symbol = "BTCUSDT", QuantityStep = 0.001m, TickSize = 0.1m, MinNotional = 50m, MaintenanceMarginRate = 0.004m }],
    };
    private readonly MarkPriceCache _marks;
    private readonly PaperFuturesService _svc;

    public PaperFuturesServiceTests()
    {
        _marks = new MarkPriceCache(_clock, _options);
        _svc = new PaperFuturesService(_store, _marks, new PaperFuturesLock(), _options, _clock,
            NullLogger<PaperFuturesService>.Instance);
        SetMark(100_000m);
    }

    private void SetMark(decimal price) => _marks.Set(new MarkPrice("BTCUSDT", price, _clock.UtcNow));

    private static OpenPositionRequest Long(decimal margin = 100m, int leverage = 10, decimal stop = 95_000m,
        decimal? tp = 110_000m, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "BTCUSDT", PositionSide.Long, leverage, margin, stop, tp);

    private decimal Wallet => _store.Ledger.Sum(entry => entry.Amount);

    [Fact]
    public async Task Open_long_computes_quantity_margin_fee_and_liquidation()
    {
        var result = await _svc.OpenAsync(Long(), default);

        var position = Assert.IsType<FuturesPosition>(result.Position);
        Assert.Equal(0.01m, position.Quantity);          // 100 * 10 / 100000
        Assert.Equal(100m, position.Margin);
        Assert.Equal(0.5m, position.OpenFee);            // 1000 notional * 0.05%
        Assert.Equal(90361.4m, position.LiquidationPrice);
        Assert.Equal(1000m - 0.5m, Wallet);
    }

    [Fact]
    public async Task Same_command_id_does_not_open_twice()
    {
        var id = Guid.NewGuid();
        await _svc.OpenAsync(Long(id: id), default);
        var second = await _svc.OpenAsync(Long(id: id), default);

        Assert.True(second.Duplicate);
        Assert.Single(_store.Positions);
    }

    [Theory]
    [InlineData(101_000, "thấp hơn giá vào")]     // SL sai phía
    [InlineData(90_000, "trước giá thanh lý")]     // SL nằm sau giá thanh lý
    public async Task Rejects_invalid_stop(decimal stop, string expected)
    {
        var result = await _svc.OpenAsync(Long(stop: stop), default);

        Assert.Null(result.Position);
        Assert.Contains(result.Quote.Errors, error => error.Contains(expected));
    }

    [Fact]
    public async Task Rejects_leverage_above_limit_and_insufficient_balance_and_min_notional()
    {
        Assert.Null((await _svc.OpenAsync(Long(leverage: 20), default)).Position);
        Assert.Null((await _svc.OpenAsync(Long(margin: 2000m, leverage: 1, stop: 1m), default)).Position);

        // Giá 30000: 4 * 10 / 30000 = 0.00133 -> 0.001 BTC = 30 USDT < 50 USDT tối thiểu.
        SetMark(30_000m);
        Assert.Contains((await _svc.QuoteAsync(Long(margin: 4m, stop: 29_000m, tp: null), default)).Errors,
            error => error.Contains("tối thiểu"));
    }

    [Fact]
    public async Task Rejects_when_mark_price_is_stale()
    {
        _clock.UtcNow = T0 + TimeSpan.FromMinutes(1);

        var result = await _svc.OpenAsync(Long(), default);

        Assert.Null(result.Position);
        Assert.Contains(result.Quote.Errors, error => error.Contains("stale"));
    }

    [Fact]
    public async Task Rejects_second_position_on_same_symbol()
    {
        await _svc.OpenAsync(Long(), default);

        var second = await _svc.OpenAsync(Long(), default);

        Assert.Null(second.Position);
    }

    [Fact]
    public async Task Stop_loss_closes_and_books_pnl_and_fees()
    {
        await _svc.OpenAsync(Long(), default);
        SetMark(94_000m); // gap qua SL 95000: khớp tại mark, không phải tại giá stop

        Assert.Equal(1, await _svc.EvaluateTriggersAsync(default));

        var position = _store.Positions.Single();
        Assert.Equal(CloseReason.StopLoss, position.CloseReason);
        Assert.Equal(-60m, position.RealizedPnl);        // 0.01 * (94000 - 100000)
        Assert.Equal(0.47m, position.CloseFee);
        Assert.Equal(1000m - 0.5m - 60m - 0.47m, Wallet);
    }

    [Fact]
    public async Task Liquidation_is_checked_before_stop_and_loses_exactly_margin()
    {
        await _svc.OpenAsync(Long(), default);
        SetMark(90_000m); // vượt cả SL và giá thanh lý 90361.4

        await _svc.EvaluateTriggersAsync(default);

        var position = _store.Positions.Single();
        Assert.Equal(PositionStatus.Liquidated, position.Status);
        Assert.Equal(-100m, position.RealizedPnl);
        Assert.Equal(1000m - 0.5m - 100m, Wallet);
    }

    [Fact]
    public async Task Short_take_profit_triggers_on_price_drop()
    {
        await _svc.OpenAsync(new OpenPositionRequest(Guid.NewGuid(), "BTCUSDT", PositionSide.Short, 5, 100m,
            105_000m, 95_000m), default);
        SetMark(94_900m);

        await _svc.EvaluateTriggersAsync(default);

        var position = _store.Positions.Single();
        Assert.Equal(CloseReason.TakeProfit, position.CloseReason);
        Assert.Equal(25.5m, position.RealizedPnl); // 0.005 * (100000 - 94900)
    }

    [Fact]
    public async Task Manual_close_is_idempotent_per_command()
    {
        var open = await _svc.OpenAsync(Long(), default);
        var cmd = Guid.NewGuid();
        SetMark(101_000m);

        Assert.True((await _svc.CloseAsync(open.Position!.Id, cmd, default)).Ok);
        Assert.True((await _svc.CloseAsync(open.Position!.Id, cmd, default)).Ok);
        Assert.False((await _svc.CloseAsync(open.Position!.Id, Guid.NewGuid(), default)).Ok);
        Assert.Equal(2, _store.Ledger.Count(entry => entry.Type == LedgerEntryType.Fee));
    }

    [Fact]
    public async Task Account_reports_unrealized_pnl_and_available_balance()
    {
        await _svc.OpenAsync(Long(), default);
        SetMark(102_000m);

        var account = await _svc.GetAccountAsync(default);

        Assert.Equal(999.5m, account.WalletBalance);
        Assert.Equal(20m, account.UnrealizedPnl);
        Assert.Equal(1019.5m, account.Equity);
        Assert.Equal(899.5m, account.AvailableBalance);
        Assert.Equal(20m, account.OpenPositions.Single().RoePercent);
    }
}
