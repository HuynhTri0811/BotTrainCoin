using System.Collections.Concurrent;
using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

public sealed class FuturesSymbolOptions
{
    public string Symbol { get; set; } = "";
    public decimal QuantityStep { get; set; }
    public decimal TickSize { get; set; }
    public decimal MinNotional { get; set; }
    public decimal MaintenanceMarginRate { get; set; }

    public FuturesSymbolRules ToRules() => new(Symbol, QuantityStep, TickSize, MinNotional, MaintenanceMarginRate);
}

public sealed class PaperFuturesOptions
{
    public decimal StartingBalance { get; set; } = 1000m;
    public decimal TakerFeeRate { get; set; } = 0.0005m;
    public decimal SlippageRate { get; set; } = 0.0005m;
    public int MaxLeverage { get; set; } = 10;
    public TimeSpan MarkPriceMaxAge { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan MarkPollInterval { get; set; } = TimeSpan.FromSeconds(2);
    public List<FuturesSymbolOptions> Symbols { get; set; } = [];

    public FuturesSymbolRules? FindRules(string symbol) =>
        Symbols.FirstOrDefault(s => s.Symbol == symbol)?.ToRules();
}

public sealed record MarkPrice(string Symbol, decimal Price, DateTime TimeUtc);

public interface IMarkPriceClient
{
    Task<MarkPrice> GetMarkPriceAsync(string symbol, CancellationToken ct);
}

/// <summary>Mark price mới nhất trong bộ nhớ. Không dùng giá cũ hơn MarkPriceMaxAge để khớp lệnh.</summary>
public sealed class MarkPriceCache(IClock clock, PaperFuturesOptions options)
{
    private readonly ConcurrentDictionary<string, MarkPrice> _prices = new();

    public void Set(MarkPrice price) => _prices[price.Symbol] = price;

    public MarkPrice? Get(string symbol) => _prices.GetValueOrDefault(symbol);

    public MarkPrice? GetFresh(string symbol) =>
        Get(symbol) is { } p && clock.UtcNow - p.TimeUtc <= options.MarkPriceMaxAge ? p : null;
}

/// <summary>Mọi thay đổi số dư/vị thế paper futures đi qua một khóa: API và worker không ghi chồng nhau.</summary>
public sealed class PaperFuturesLock
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}

/// <summary>Điều kiện lọc vị thế đã đóng; trường null là không lọc. Thời gian tính theo giờ đóng, [FromUtc, ToUtc).</summary>
public sealed record ClosedPositionFilter(
    string? Symbol = null,
    PositionSide? Side = null,
    CloseReason? CloseReason = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);

public interface IPaperFuturesStore
{
    Task<IReadOnlyList<FuturesPosition>> GetOpenPositionsAsync(CancellationToken ct);
    Task<FuturesPosition?> GetAsync(long id, CancellationToken ct);
    Task<FuturesPosition?> FindByOpenCommandAsync(Guid commandId, CancellationToken ct);
    Task<IReadOnlyList<FuturesPosition>> GetClosedAsync(ClosedPositionFilter filter, int limit, CancellationToken ct);
    Task<IReadOnlyList<PaperLedgerEntry>> GetLedgerAsync(CancellationToken ct);
    void AddPosition(FuturesPosition position);
    void AddLedger(PaperLedgerEntry entry);

    /// <summary>Một lần gọi = một transaction (vị thế + bút toán cùng thành công hoặc cùng rollback).</summary>
    Task SaveAsync(CancellationToken ct);
}

public sealed record OpenPositionRequest(
    Guid CommandId,
    string Symbol,
    PositionSide Side,
    int Leverage,
    decimal Margin,
    decimal StopLoss,
    decimal? TakeProfit);

public sealed record OpenQuote(
    bool Accepted,
    IReadOnlyList<string> Errors,
    string Symbol,
    PositionSide Side,
    int Leverage,
    decimal? MarkPrice,
    decimal? FillPrice,
    decimal? Quantity,
    decimal? Notional,
    decimal? Margin,
    decimal? OpenFee,
    decimal? LiquidationPrice,
    decimal? LossAtStop,
    decimal? ProfitAtTakeProfit,
    decimal AvailableBalance);

public sealed record OpenResult(OpenQuote Quote, FuturesPosition? Position, bool Duplicate);

public sealed record CloseResult(bool Ok, string? Error, FuturesPosition? Position);

public sealed record PositionView(FuturesPosition Position, decimal? MarkPrice, decimal? UnrealizedPnl, decimal? RoePercent);

public sealed record AccountSnapshot(
    decimal WalletBalance,
    decimal UsedMargin,
    decimal UnrealizedPnl,
    decimal Equity,
    decimal AvailableBalance,
    IReadOnlyList<PositionView> OpenPositions,
    IReadOnlyList<MarkPrice> MarkPrices);
