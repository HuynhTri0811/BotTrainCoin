namespace CryptoBot.Domain.Futures;

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
