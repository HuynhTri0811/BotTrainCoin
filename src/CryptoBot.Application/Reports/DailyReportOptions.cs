using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Reports;

public sealed class DailyReportOptions
{
    /// <summary>Giờ lập báo cáo mỗi ngày, theo <see cref="TimeZone"/>.</summary>
    public TimeSpan RunAt { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Múi giờ IANA của <see cref="RunAt"/>.</summary>
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";

    /// <summary>Độ dài khoảng báo cáo, kết thúc đúng lúc lập báo cáo.</summary>
    public TimeSpan Period { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Khung nến Binance dùng để lập báo cáo.</summary>
    public string Interval { get; set; } = "1h";

    /// <summary>Số nến trước mỗi nến dùng làm trung bình khối lượng.</summary>
    public int BaselineCandles { get; set; } = DailyMoveReport.DefaultBaselineCandles;

    /// <summary>Khối lượng gấp ngần này lần trung bình thì coi là bất thường.</summary>
    public decimal VolumeSpikeMultiplier { get; set; } = DailyMoveReport.DefaultVolumeSpikeMultiplier;

    /// <summary>Sàn lỗi thì thử lại sau khoảng này thay vì chờ tới hôm sau.</summary>
    public TimeSpan RetryAfter { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Symbol Binance cần báo cáo, ví dụ "BTCUSDT".</summary>
    public List<string> Symbols { get; set; } = [];
}
