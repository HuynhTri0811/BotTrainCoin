namespace CryptoBot.Domain.MarketData;

/// <summary>
/// Báo cáo biến động đã lưu của một symbol cho một khoảng thời gian. Nội dung (<see cref="DailyMoveReport"/>) lưu
/// dạng JSON vì chỉ dùng để xem lại, không truy vấn theo từng con số. Khóa duy nhất: (Symbol, PeriodEndUtc).
/// </summary>
public sealed class DailyReport
{
    /// <summary>Khóa chính do database cấp.</summary>
    public long Id { get; private set; }

    /// <summary>Cặp giao dịch, ví dụ "BTCUSDT".</summary>
    public string Symbol { get; private set; } = null!;

    /// <summary>Mã khung nến dùng để lập báo cáo, ví dụ "1h".</summary>
    public string Interval { get; private set; } = null!;

    /// <summary>Đầu khoảng báo cáo (bao gồm), UTC.</summary>
    public DateTime PeriodStartUtc { get; private set; }

    /// <summary>Cuối khoảng báo cáo (không bao gồm), UTC. Cũng là giờ báo cáo được lên lịch.</summary>
    public DateTime PeriodEndUtc { get; private set; }

    /// <summary>Thời điểm bot lập báo cáo, UTC (có thể trễ hơn <see cref="PeriodEndUtc"/> khi tạo bù).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary><see cref="DailyMoveReport"/> dạng JSON.</summary>
    public string ContentJson { get; private set; } = null!;

    /// <summary>Dành cho EF Core khi đọc từ database.</summary>
    private DailyReport() { }

    public DailyReport(string symbol, string interval, DateTime periodStartUtc, DateTime periodEndUtc,
        DateTime createdAtUtc, string contentJson)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Thiếu symbol.", nameof(symbol));
        if (periodEndUtc <= periodStartUtc)
            throw new ArgumentException("Cuối khoảng phải sau đầu khoảng.", nameof(periodEndUtc));
        if (string.IsNullOrWhiteSpace(contentJson))
            throw new ArgumentException("Thiếu nội dung báo cáo.", nameof(contentJson));

        Symbol = symbol;
        Interval = interval;
        PeriodStartUtc = periodStartUtc;
        PeriodEndUtc = periodEndUtc;
        CreatedAtUtc = createdAtUtc;
        ContentJson = contentJson;
    }
}
