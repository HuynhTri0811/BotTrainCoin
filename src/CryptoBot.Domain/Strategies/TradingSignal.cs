namespace CryptoBot.Domain.Strategies;

/// <summary>
/// Tín hiệu BUY/SELL đã phát. Khóa duy nhất: (StrategyInstanceId, ConfigVersion, CandleOpenTimeUtc, Action).
/// </summary>
public sealed class TradingSignal
{
    public long Id { get; private set; }
    public int StrategyInstanceId { get; private set; }
    public int ConfigVersion { get; private set; }
    public string Symbol { get; private set; } = null!;
    public string Interval { get; private set; } = null!;
    public DateTime CandleOpenTimeUtc { get; private set; }
    public SignalAction Action { get; private set; }
    public string Reason { get; private set; } = null!;

    /// <summary>Giá đóng cửa của nến tạo tín hiệu (chỉ để tham chiếu, không phải giá khớp).</summary>
    public decimal CandleClose { get; private set; }
    public decimal FastEma { get; private set; }
    public decimal SlowEma { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>
    /// True khi tín hiệu sinh ra từ nến cũ (tải bù/khởi động). Tín hiệu backfill chỉ dùng cho
    /// lịch sử và chart, không bao giờ được dùng để đặt lệnh.
    /// </summary>
    public bool IsBackfill { get; private set; }

    private TradingSignal() { }

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
