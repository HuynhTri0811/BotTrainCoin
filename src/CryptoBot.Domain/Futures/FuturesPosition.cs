namespace CryptoBot.Domain.Futures;

public enum PositionStatus
{
    Open = 1,
    Closed = 2,
    Liquidated = 3,
}

public enum CloseReason
{
    Manual = 1,
    StopLoss = 2,
    TakeProfit = 3,
    Liquidation = 4,
}

/// <summary>
/// Vị thế Futures giả lập, isolated margin, one-way mode (mỗi symbol tối đa một vị thế mở).
/// Môi trường luôn là Paper; tiền thật không đi qua entity này.
/// </summary>
public sealed class FuturesPosition
{
    public long Id { get; private set; }
    public string Environment { get; private set; } = "Paper";
    public Guid OpenCommandId { get; private set; }
    public Guid? CloseCommandId { get; private set; }
    public string Symbol { get; private set; } = null!;
    public PositionSide Side { get; private set; }
    public int Leverage { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal EntryPrice { get; private set; }
    public decimal Margin { get; private set; }
    public decimal LiquidationPrice { get; private set; }
    public decimal StopLoss { get; private set; }
    public decimal? TakeProfit { get; private set; }
    public decimal OpenFee { get; private set; }
    public decimal CloseFee { get; private set; }
    public PositionStatus Status { get; private set; }
    public CloseReason? CloseReason { get; private set; }
    public decimal? ExitPrice { get; private set; }

    /// <summary>PnL giá (chưa trừ phí). Bị thanh lý thì bằng −Margin.</summary>
    public decimal? RealizedPnl { get; private set; }
    public DateTime OpenedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

    private FuturesPosition() { }

    public FuturesPosition(Guid openCommandId, string symbol, PositionSide side, int leverage, decimal quantity,
        decimal entryPrice, decimal margin, decimal liquidationPrice, decimal stopLoss, decimal? takeProfit,
        decimal openFee, DateTime openedAtUtc)
    {
        OpenCommandId = openCommandId;
        Symbol = symbol;
        Side = side;
        Leverage = leverage;
        Quantity = quantity;
        EntryPrice = entryPrice;
        Margin = margin;
        LiquidationPrice = liquidationPrice;
        StopLoss = stopLoss;
        TakeProfit = takeProfit;
        OpenFee = openFee;
        Status = PositionStatus.Open;
        OpenedAtUtc = openedAtUtc;
    }

    public decimal NetPnl => (RealizedPnl ?? 0) - OpenFee - CloseFee;

    public void Close(CloseReason reason, decimal exitPrice, decimal closeFee, Guid? commandId, DateTime nowUtc)
    {
        EnsureOpen();
        Status = PositionStatus.Closed;
        CloseReason = reason;
        ExitPrice = exitPrice;
        CloseFee = closeFee;
        CloseCommandId = commandId;
        // Isolated: lỗ giá không vượt quá margin của vị thế.
        RealizedPnl = Math.Max(FuturesMath.UnrealizedPnl(Side, Quantity, EntryPrice, exitPrice), -Margin);
        ClosedAtUtc = nowUtc;
    }

    /// <summary>Isolated: mất toàn bộ margin của vị thế, không hơn.</summary>
    public void Liquidate(decimal markPrice, DateTime nowUtc)
    {
        EnsureOpen();
        Status = PositionStatus.Liquidated;
        CloseReason = Futures.CloseReason.Liquidation;
        ExitPrice = markPrice;
        RealizedPnl = -Margin;
        ClosedAtUtc = nowUtc;
    }

    private void EnsureOpen()
    {
        if (Status != PositionStatus.Open)
            throw new InvalidOperationException($"Vị thế {Id} đã {Status}.");
    }
}

public enum LedgerEntryType
{
    Deposit = 1,
    Fee = 2,
    RealizedPnl = 3,
}

/// <summary>Bút toán sổ vốn giả lập. Số dư ví = tổng Amount (tính trong ứng dụng, không SUM bằng SQL).</summary>
public sealed class PaperLedgerEntry
{
    public long Id { get; private set; }
    public string Environment { get; private set; } = "Paper";
    public string Asset { get; private set; } = "USDT";
    public decimal Amount { get; private set; }
    public LedgerEntryType Type { get; private set; }
    public long? PositionId { get; private set; }
    public FuturesPosition? Position { get; private set; }
    public string Note { get; private set; } = "";
    public DateTime TimeUtc { get; private set; }

    private PaperLedgerEntry() { }

    public PaperLedgerEntry(decimal amount, LedgerEntryType type, FuturesPosition? position, string note, DateTime timeUtc)
    {
        Amount = amount;
        Type = type;
        Position = position;
        Note = note;
        TimeUtc = timeUtc;
    }
}
