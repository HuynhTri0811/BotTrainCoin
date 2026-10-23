namespace CryptoBot.Domain.Strategies;

/// <summary>
/// Tín hiệu BUY/SELL đã phát. Khóa duy nhất: (StrategyInstanceId, ConfigVersion, CandleOpenTimeUtc, Action).
/// </summary>
public sealed class TradingSignal
{
    /// <summary>Khóa chính do database cấp.</summary>
    public long Id { get; private set; }

    /// <summary>Strategy instance đã phát tín hiệu.</summary>
    public int StrategyInstanceId { get; private set; }

    /// <summary>Phiên bản cấu hình của instance lúc phát tín hiệu.</summary>
    public int ConfigVersion { get; private set; }

    /// <summary>Cặp giao dịch, ví dụ "BTCUSDT".</summary>
    public string Symbol { get; private set; } = null!;

    /// <summary>Mã khung nến, ví dụ "1h".</summary>
    public string Interval { get; private set; } = null!;

    /// <summary>Thời điểm mở của nến (đã đóng) tạo ra tín hiệu, UTC.</summary>
    public DateTime CandleOpenTimeUtc { get; private set; }

    /// <summary>BUY hoặc SELL (Hold không được lưu).</summary>
    public SignalAction Action { get; private set; }

    /// <summary>Lý do dạng chữ, ví dụ "EMA9 cắt lên EMA21.".</summary>
    public string Reason { get; private set; } = null!;

    /// <summary>Giá đóng cửa của nến tạo tín hiệu (chỉ để tham chiếu, không phải giá khớp).</summary>
    public decimal CandleClose { get; private set; }

    /// <summary>Giá trị EMA nhanh tại nến tạo tín hiệu.</summary>
    public decimal FastEma { get; private set; }

    /// <summary>Giá trị EMA chậm tại nến tạo tín hiệu.</summary>
    public decimal SlowEma { get; private set; }

    /// <summary>Thời điểm bot tạo tín hiệu (UTC), khác với thời điểm của nến.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// True khi tín hiệu sinh ra từ nến cũ (tải bù/khởi động). Tín hiệu backfill chỉ dùng cho
    /// lịch sử và chart, không bao giờ được dùng để đặt lệnh.
    /// </summary>
    public bool IsBackfill { get; private set; }

    /// <summary>Dành cho EF Core khi đọc từ database.</summary>
    private TradingSignal() { }

    /// <summary>Tạo tín hiệu BUY/SELL; không cho phép tạo tín hiệu Hold.</summary>
    public TradingSignal(int strategyInstanceId, int configVersion, string symbol, string interval,
        DateTime candleOpenTimeUtc, SignalAction action, string reason, decimal candleClose,
        decimal fastEma, decimal slowEma, DateTime createdAtUtc, bool isBackfill)
    {
        if (action == SignalAction.Hold)
            throw new ArgumentException("Không lưu Hold thành signal.", nameof(action));

        StrategyInstanceId = strategyInstanceId;
        ConfigVersion = configVersion;
        Symbol = symbol;
        Interval = interval;
        CandleOpenTimeUtc = candleOpenTimeUtc;
        Action = action;
        Reason = reason;
        CandleClose = candleClose;
        FastEma = fastEma;
        SlowEma = slowEma;
        CreatedAtUtc = createdAtUtc;
        IsBackfill = isBackfill;
    }
}
