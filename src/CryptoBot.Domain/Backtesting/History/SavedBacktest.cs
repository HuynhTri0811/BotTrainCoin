namespace CryptoBot.Domain.Backtesting;

/// <summary>
/// Một lần backtest đã lưu để xem lại và so sánh. Thông số và các con số chính nằm ở cột riêng để liệt kê nhanh;
/// toàn bộ kết quả (lệnh, chart) nằm trong <see cref="Content"/> dạng JSON nén gzip vì chỉ dùng để mở lại, không
/// truy vấn theo từng con số. Các cột decimal chỉ để hiển thị (nguyên tắc B1: không so sánh/sắp xếp trong SQL).
/// </summary>
public sealed class SavedBacktest
{
    /// <summary>Độ dài tối đa của ghi chú.</summary>
    public const int MaxNoteLength = 500;

    /// <summary>Khóa chính do database cấp.</summary>
    public long Id { get; private set; }

    /// <summary>Lúc chạy, UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Strategy instance đã chạy (cho biết sàn/cặp/khung nến).</summary>
    public int StrategyInstanceId { get; private set; }

    /// <summary>Cặp giao dịch, ví dụ "BTCUSDT".</summary>
    public string Symbol { get; private set; } = null!;

    /// <summary>Mã khung nến, ví dụ "1h".</summary>
    public string Interval { get; private set; } = null!;

    /// <summary>Số nến EMA nhanh đã dùng.</summary>
    public int FastPeriod { get; private set; }

    /// <summary>Số nến EMA chậm đã dùng.</summary>
    public int SlowPeriod { get; private set; }

    /// <summary>Cửa sổ nến đã dùng.</summary>
    public int LookbackCandles { get; private set; }

    /// <summary>Nến đầu tiên được giao dịch; null khi khoảng không có nến.</summary>
    public DateTime? FirstCandleUtc { get; private set; }

    /// <summary>Nến cuối; null khi khoảng không có nến.</summary>
    public DateTime? LastCandleUtc { get; private set; }

    /// <summary>Vốn ban đầu.</summary>
    public decimal InitialCapital { get; private set; }

    /// <summary>Cách tính khối lượng mỗi lệnh.</summary>
    public PositionSizing Sizing { get; private set; }

    /// <summary>% vốn hoặc số tiền cố định mỗi lệnh.</summary>
    public decimal SizeValue { get; private set; }

    /// <summary>Chốt lời %; null = không dùng.</summary>
    public decimal? TakeProfitPercent { get; private set; }

    /// <summary>Cắt lỗ %; null = không dùng.</summary>
    public decimal? StopLossPercent { get; private set; }

    /// <summary>Phí % mỗi lần khớp.</summary>
    public decimal FeePercent { get; private set; }

    /// <summary>Số lệnh đã đóng.</summary>
    public int TotalTrades { get; private set; }

    /// <summary>Tỷ lệ thắng %.</summary>
    public decimal WinRatePercent { get; private set; }

    /// <summary>Lợi nhuận ròng sau phí.</summary>
    public decimal NetProfit { get; private set; }

    /// <summary>Lợi nhuận ròng so với vốn ban đầu, %.</summary>
    public decimal NetProfitPercent { get; private set; }

    /// <summary>Sụt giảm tối đa, %.</summary>
    public decimal MaxDrawdownPercent { get; private set; }

    /// <summary>Ghi chú của người dùng; null khi chưa ghi.</summary>
    public string? Note { get; private set; }

    /// <summary>Toàn bộ kết quả dạng JSON nén gzip.</summary>
    public byte[] Content { get; private set; } = null!;

    /// <summary>Dành cho EF Core khi đọc từ database.</summary>
    private SavedBacktest() { }

    /// <summary>Tạo bản lưu từ thông số và kết quả của một lần chạy.</summary>
    /// <exception cref="ArgumentException">Thiếu symbol/khung nến hoặc nội dung.</exception>
    public SavedBacktest(DateTime createdAtUtc, int strategyInstanceId, string symbol, string interval,
        int fastPeriod, int slowPeriod, int lookbackCandles, DateTime? firstCandleUtc, DateTime? lastCandleUtc,
        BacktestSettings settings, BacktestStatistics statistics, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(interval);
        if (content is null || content.Length == 0)
            throw new ArgumentException("Thiếu nội dung kết quả.", nameof(content));

        CreatedAtUtc = createdAtUtc;
        StrategyInstanceId = strategyInstanceId;
        Symbol = symbol;
        Interval = interval;
        FastPeriod = fastPeriod;
        SlowPeriod = slowPeriod;
        LookbackCandles = lookbackCandles;
        FirstCandleUtc = firstCandleUtc;
        LastCandleUtc = lastCandleUtc;
        InitialCapital = settings.InitialCapital;
        Sizing = settings.Sizing;
        SizeValue = settings.SizeValue;
        TakeProfitPercent = settings.TakeProfitPercent;
        StopLossPercent = settings.StopLossPercent;
        FeePercent = settings.FeePercent;
        TotalTrades = statistics.TotalTrades;
        WinRatePercent = statistics.WinRatePercent;
        NetProfit = statistics.NetProfit;
        NetProfitPercent = statistics.NetProfitPercent;
        MaxDrawdownPercent = statistics.MaxDrawdownPercent;
        Content = content;
    }

    /// <summary>Thông số vốn/rủi ro đã dùng, dựng lại từ các cột.</summary>
    public BacktestSettings Settings =>
        new(InitialCapital, Sizing, SizeValue, TakeProfitPercent, StopLossPercent, FeePercent);

    /// <summary>Đặt hoặc xóa ghi chú (chuỗi trống = xóa). Khoảng trắng đầu/cuối bị bỏ.</summary>
    /// <exception cref="ArgumentException">Ghi chú dài hơn <see cref="MaxNoteLength"/> ký tự.</exception>
    public void SetNote(string? note)
    {
        string? trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed is { Length: > MaxNoteLength })
            throw new ArgumentException($"Ghi chú tối đa {MaxNoteLength} ký tự.", nameof(note));
        Note = trimmed;
    }
}
