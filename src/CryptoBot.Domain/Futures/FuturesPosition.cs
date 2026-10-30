namespace CryptoBot.Domain.Futures;

/// <summary>
/// Vị thế Futures giả lập, isolated margin, one-way mode (mỗi symbol tối đa một vị thế mở).
/// Môi trường luôn là Paper; tiền thật không đi qua entity này.
/// </summary>
public sealed class FuturesPosition
{
    /// <summary>Khóa chính do database cấp.</summary>
    public long Id { get; private set; }

    /// <summary>Môi trường chạy; luôn là "Paper".</summary>
    public string Environment { get; private set; } = "Paper";

    /// <summary>Mã lệnh mở (duy nhất). Gửi lại cùng mã thì trả về vị thế cũ, không mở thêm.</summary>
    public Guid OpenCommandId { get; private set; }

    /// <summary>Mã lệnh đóng tay; null khi đóng tự động (SL/TP/thanh lý) hoặc chưa đóng.</summary>
    public Guid? CloseCommandId { get; private set; }

    /// <summary>Cặp giao dịch, ví dụ "BTCUSDT".</summary>
    public string Symbol { get; private set; } = null!;

    /// <summary>Long (lời khi giá tăng) hoặc Short (lời khi giá giảm).</summary>
    public PositionSide Side { get; private set; }

    /// <summary>Đòn bẩy, ví dụ 3 = 3x.</summary>
    public int Leverage { get; private set; }

    /// <summary>Khối lượng (đơn vị coin, ví dụ BTC), đã làm tròn xuống theo bước khối lượng của sàn.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Giá vào lệnh thực tế (mark price cộng trượt giá bất lợi).</summary>
    public decimal EntryPrice { get; private set; }

    /// <summary>Tiền ký quỹ (USDT) = Quantity × EntryPrice / Leverage; isolated nên đây là mức lỗ tối đa.</summary>
    public decimal Margin { get; private set; }

    /// <summary>Giá mà tại đó vị thế bị thanh lý.</summary>
    public decimal LiquidationPrice { get; private set; }

    /// <summary>Giá cắt lỗ; luôn nằm trước giá thanh lý.</summary>
    public decimal StopLoss { get; private set; }

    /// <summary>Giá chốt lời; null = không đặt.</summary>
    public decimal? TakeProfit { get; private set; }

    /// <summary>Phí mở vị thế (USDT).</summary>
    public decimal OpenFee { get; private set; }

    /// <summary>Phí đóng vị thế (USDT); 0 khi chưa đóng hoặc bị thanh lý.</summary>
    public decimal CloseFee { get; private set; }

    /// <summary>Trạng thái hiện tại.</summary>
    public PositionStatus Status { get; private set; }

    /// <summary>Lý do kết thúc; null khi đang mở.</summary>
    public CloseReason? CloseReason { get; private set; }

    /// <summary>Giá thoát lệnh; null khi đang mở.</summary>
    public decimal? ExitPrice { get; private set; }

    /// <summary>PnL giá (chưa trừ phí). Bị thanh lý thì bằng −Margin.</summary>
    public decimal? RealizedPnl { get; private set; }

    /// <summary>Thời điểm mở vị thế (UTC).</summary>
    public DateTime OpenedAtUtc { get; private set; }

    /// <summary>Thời điểm kết thúc (UTC); null khi đang mở.</summary>
    public DateTime? ClosedAtUtc { get; private set; }

    /// <summary>Dành cho EF Core khi đọc từ database.</summary>
    private FuturesPosition() { }

    /// <summary>Tạo vị thế mới ở trạng thái đang mở. Các giá trị đã được engine tính và kiểm tra trước.</summary>
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

    /// <summary>Lãi/lỗ ròng = PnL giá − phí mở − phí đóng (không lưu vào database).</summary>
    public decimal NetPnl => (RealizedPnl ?? 0) - OpenFee - CloseFee;

    /// <summary>Đóng vị thế tại <paramref name="exitPrice"/> (tay, SL, TP hoặc tín hiệu đảo chiều) và ghi PnL giá.</summary>
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

    /// <summary>Chặn đóng/thanh lý hai lần.</summary>
    private void EnsureOpen()
    {
        if (Status != PositionStatus.Open)
            throw new InvalidOperationException($"Vị thế {Id} đã {Status}.");
    }
}

/// <summary>Bút toán sổ vốn giả lập. Số dư ví = tổng Amount (tính trong ứng dụng, không SUM bằng SQL).</summary>
public sealed class PaperLedgerEntry
{
    /// <summary>Khóa chính do database cấp.</summary>
    public long Id { get; private set; }

    /// <summary>Môi trường chạy; luôn là "Paper".</summary>
    public string Environment { get; private set; } = "Paper";

    /// <summary>Loại tài sản; hiện luôn là "USDT".</summary>
    public string Asset { get; private set; } = "USDT";

    /// <summary>Số tiền: dương là cộng vào ví, âm là trừ khỏi ví.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Loại bút toán.</summary>
    public LedgerEntryType Type { get; private set; }

    /// <summary>Id vị thế liên quan; null với bút toán nạp vốn.</summary>
    public long? PositionId { get; private set; }

    /// <summary>Vị thế liên quan (navigation của EF Core).</summary>
    public FuturesPosition? Position { get; private set; }

    /// <summary>Mô tả ngắn, ví dụ "Phí mở vị thế".</summary>
    public string Note { get; private set; } = "";

    /// <summary>Thời điểm ghi bút toán (UTC); dùng để tính lỗ trong ngày.</summary>
    public DateTime TimeUtc { get; private set; }

    /// <summary>Dành cho EF Core khi đọc từ database.</summary>
    private PaperLedgerEntry() { }

    /// <summary>Tạo bút toán mới.</summary>
    public PaperLedgerEntry(decimal amount, LedgerEntryType type, FuturesPosition? position, string note, DateTime timeUtc)
    {
        Amount = amount;
        Type = type;
        Position = position;
        Note = note;
        TimeUtc = timeUtc;
    }
}
