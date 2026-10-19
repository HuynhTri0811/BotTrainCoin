namespace CryptoBot.Domain.Futures;

public enum TriggerCondition
{
    /// <summary>Kích hoạt khi mark price ≤ giá đặt (ví dụ mua khi giá giảm về một mức).</summary>
    AtOrBelow = 1,

    /// <summary>Kích hoạt khi mark price ≥ giá đặt.</summary>
    AtOrAbove = 2,
}

/// <summary>Quy tắc kích hoạt theo mức giá hay theo tín hiệu của một chỉ báo tự viết.</summary>
public enum RuleTriggerType
{
    Price = 1,
    Indicator = 2,
}

public enum AutoRuleStatus
{
    Active = 1,
    Triggered = 2,
    Rejected = 3,
    Cancelled = 4,
}

/// <summary>
/// Quy tắc "khi giá chạm mức này (hoặc chỉ báo phát tín hiệu) thì tự mở lệnh Paper". Chạy một lần: sau khi kích hoạt
/// (thành công hoặc bị từ chối) quy tắc không tự bật lại. <see cref="CommandId"/> cũng là
/// commandId của lệnh mở, nên dù bị xử lý lặp cũng không mở hai vị thế.
/// </summary>
public sealed class AutoOrderRule
{
    public long Id { get; private set; }
    public string Environment { get; private set; } = "Paper";
    public Guid CommandId { get; private set; }
    public string Symbol { get; private set; } = null!;
    public PositionSide Side { get; private set; }
    public RuleTriggerType TriggerType { get; private set; } = RuleTriggerType.Price;

    /// <summary>Chỉ có với quy tắc theo giá.</summary>
    public TriggerCondition? Condition { get; private set; }
    public decimal? TriggerPrice { get; private set; }

    /// <summary>Tên chỉ báo trong cấu hình; chỉ có với quy tắc theo chỉ báo.</summary>
    public string? IndicatorName { get; private set; }
    public decimal Margin { get; private set; }
    public int Leverage { get; private set; }

    /// <summary>Stop loss theo % so với mark price lúc kích hoạt (1 = 1%).</summary>
    public decimal StopLossPercent { get; private set; }
    public decimal? TakeProfitPercent { get; private set; }
    public AutoRuleStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? TriggeredAtUtc { get; private set; }
    public decimal? TriggeredMarkPrice { get; private set; }
    public long? PositionId { get; private set; }

    /// <summary>Giá trị chỉ báo và lý do tại nến đã kích hoạt quy tắc.</summary>
    public string? TriggerDetail { get; private set; }

    /// <summary>Lý do bị từ chối, hoặc ghi chú đang bị chặn (ví dụ chạm giới hạn lỗ ngày).</summary>
    public string? Note { get; private set; }

    private AutoOrderRule() { }

    public AutoOrderRule(string symbol, PositionSide side, TriggerCondition condition, decimal triggerPrice,
        decimal margin, int leverage, decimal stopLossPercent, decimal? takeProfitPercent, DateTime createdAtUtc)
    {
        Init(symbol, side, margin, leverage, stopLossPercent, takeProfitPercent, createdAtUtc);
        Condition = condition;
        TriggerPrice = triggerPrice;
    }

    /// <summary>
    /// Quy tắc mở lệnh khi chỉ báo <paramref name="indicatorName"/> phát tín hiệu cùng hướng
    /// (Long ↔ Long, Short ↔ Short) trên một nến đóng sau thời điểm tạo quy tắc.
    /// </summary>
    public static AutoOrderRule ForIndicator(string symbol, PositionSide side, string indicatorName,
        decimal margin, int leverage, decimal stopLossPercent, decimal? takeProfitPercent, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indicatorName);
        var rule = new AutoOrderRule { TriggerType = RuleTriggerType.Indicator, IndicatorName = indicatorName };
        rule.Init(symbol, side, margin, leverage, stopLossPercent, takeProfitPercent, createdAtUtc);
        return rule;
    }

    private void Init(string symbol, PositionSide side, decimal margin, int leverage, decimal stopLossPercent,
        decimal? takeProfitPercent, DateTime createdAtUtc)
    {
        CommandId = Guid.NewGuid();
        Symbol = symbol;
        Side = side;
        Margin = margin;
        Leverage = leverage;
        StopLossPercent = stopLossPercent;
        TakeProfitPercent = takeProfitPercent;
        Status = AutoRuleStatus.Active;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Điều kiện giá; quy tắc theo chỉ báo luôn trả về false.</summary>
    public bool IsMet(decimal markPrice) => TriggerType == RuleTriggerType.Price && (Condition == TriggerCondition.AtOrBelow
        ? markPrice <= TriggerPrice
        : markPrice >= TriggerPrice);

    public void MarkTriggered(decimal markPrice, long positionId, DateTime nowUtc, string? detail = null)
    {
        EnsureActive();
        Status = AutoRuleStatus.Triggered;
        TriggeredAtUtc = nowUtc;
        TriggeredMarkPrice = markPrice;
        PositionId = positionId;
        TriggerDetail = detail;
        Note = null;
    }

    public void MarkRejected(decimal markPrice, string reason, DateTime nowUtc, string? detail = null)
    {
        EnsureActive();
        TriggerDetail = detail;
        Status = AutoRuleStatus.Rejected;
        TriggeredAtUtc = nowUtc;
        TriggeredMarkPrice = markPrice;
        Note = reason;
    }

    public void Cancel()
    {
        EnsureActive();
        Status = AutoRuleStatus.Cancelled;
    }

    /// <summary>Ghi chú tạm khi điều kiện đã đúng nhưng quy tắc đang bị chặn; trả về true nếu có thay đổi.</summary>
    public bool SetBlockedNote(string? note)
    {
        EnsureActive();
        if (Note == note)
            return false;
        Note = note;
        return true;
    }

    private void EnsureActive()
    {
        if (Status != AutoRuleStatus.Active)
            throw new InvalidOperationException($"Quy tắc {Id} đã {Status}.");
    }
}
