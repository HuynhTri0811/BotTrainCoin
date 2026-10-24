namespace CryptoBot.Domain.Futures;

/// <summary>Hướng so sánh giữa mark price và giá đặt của quy tắc theo giá.</summary>
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
    /// <summary>Kích hoạt khi mark price chạm mức giá đặt.</summary>
    Price = 1,

    /// <summary>Kích hoạt khi chỉ báo tự viết phát tín hiệu cùng hướng với quy tắc.</summary>
    Indicator = 2,
}

/// <summary>Trạng thái của quy tắc. Chỉ trạng thái <see cref="Active"/> mới còn được kiểm tra.</summary>
public enum AutoRuleStatus
{
    /// <summary>Đang chờ điều kiện xảy ra.</summary>
    Active = 1,

    /// <summary>Đã kích hoạt và mở được vị thế.</summary>
    Triggered = 2,

    /// <summary>Điều kiện đã xảy ra nhưng engine từ chối mở lệnh; lý do nằm trong <see cref="AutoOrderRule.Note"/>.</summary>
    Rejected = 3,

    /// <summary>Người dùng đã hủy trước khi kích hoạt.</summary>
    Cancelled = 4,
}

/// <summary>
/// Quy tắc "khi giá chạm mức này (hoặc chỉ báo phát tín hiệu) thì tự mở lệnh Paper". Chạy một lần: sau khi kích hoạt
/// (thành công hoặc bị từ chối) quy tắc không tự bật lại. <see cref="CommandId"/> cũng là
/// commandId của lệnh mở, nên dù bị xử lý lặp cũng không mở hai vị thế.
/// Quy tắc theo chỉ báo có thể bật <see cref="CloseOnReverse"/> để tự đóng vị thế đã mở khi chỉ báo đảo chiều.
/// </summary>
public sealed class AutoOrderRule
{
    /// <summary>Khóa chính do database cấp.</summary>
    public long Id { get; private set; }

    /// <summary>Môi trường chạy; luôn là "Paper" (không có đường tới tiền thật).</summary>
    public string Environment { get; private set; } = "Paper";

    /// <summary>Mã lệnh duy nhất, dùng lại làm commandId khi mở vị thế để chống mở trùng.</summary>
    public Guid CommandId { get; private set; }

    /// <summary>Cặp giao dịch, ví dụ "BTCUSDT".</summary>
    public string Symbol { get; private set; } = null!;

    /// <summary>Hướng vị thế sẽ mở khi kích hoạt: Long hoặc Short.</summary>
    public PositionSide Side { get; private set; }

    /// <summary>Loại điều kiện: theo mức giá hay theo tín hiệu chỉ báo.</summary>
    public RuleTriggerType TriggerType { get; private set; } = RuleTriggerType.Price;

    /// <summary>Chỉ có với quy tắc theo giá.</summary>
    public TriggerCondition? Condition { get; private set; }

    /// <summary>Mức giá kích hoạt (so với mark price); chỉ có với quy tắc theo giá.</summary>
    public decimal? TriggerPrice { get; private set; }

    /// <summary>Tên chỉ báo trong cấu hình; chỉ có với quy tắc theo chỉ báo.</summary>
    public string? IndicatorName { get; private set; }

    /// <summary>Số tiền ký quỹ (USDT) cho vị thế sẽ mở.</summary>
    public decimal Margin { get; private set; }

    /// <summary>Đòn bẩy, ví dụ 2 = 2x.</summary>
    public int Leverage { get; private set; }

    /// <summary>Stop loss theo % so với mark price lúc kích hoạt (1 = 1%).</summary>
    public decimal StopLossPercent { get; private set; }

    /// <summary>Take profit theo % so với mark price lúc kích hoạt; null = không đặt.</summary>
    public decimal? TakeProfitPercent { get; private set; }

    /// <summary>Trạng thái hiện tại của quy tắc.</summary>
    public AutoRuleStatus Status { get; private set; }

    /// <summary>Thời điểm tạo quy tắc (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Thời điểm điều kiện xảy ra và quy tắc được xử lý (UTC); null khi chưa kích hoạt.</summary>
    public DateTime? TriggeredAtUtc { get; private set; }

    /// <summary>Mark price lúc kích hoạt; SL/TP được tính theo % từ giá này.</summary>
    public decimal? TriggeredMarkPrice { get; private set; }

    /// <summary>Id vị thế đã mở khi kích hoạt thành công.</summary>
    public long? PositionId { get; private set; }

    /// <summary>Giá trị chỉ báo và lý do tại nến đã kích hoạt quy tắc.</summary>
    public string? TriggerDetail { get; private set; }

    /// <summary>Lý do bị từ chối, hoặc ghi chú đang bị chặn (ví dụ chạm giới hạn lỗ ngày).</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Chỉ với quy tắc theo chỉ báo: sau khi mở lệnh, tự đóng vị thế khi chính chỉ báo đó phát tín hiệu
    /// ngược hướng (Long đang mở mà chỉ báo báo Short, và ngược lại).
    /// </summary>
    public bool CloseOnReverse { get; private set; }

    /// <summary>Mã lệnh đóng, dùng làm commandId khi tự đóng để dù xử lý lặp cũng không đóng hai lần.</summary>
    public Guid? CloseCommandId { get; private set; }

    /// <summary>Thời điểm ngừng theo dõi đảo chiều (đã tự đóng, hoặc vị thế đã kết thúc theo cách khác).</summary>
    public DateTime? CloseWatchEndedAtUtc { get; private set; }

    /// <summary>Kết quả theo dõi đảo chiều: tín hiệu đã đóng vị thế, hoặc lý do vị thế đã kết thúc trước.</summary>
    public string? CloseDetail { get; private set; }

    /// <summary>Lệnh đã mở và vẫn đang chờ tín hiệu đảo chiều để tự đóng.</summary>
    public bool IsWatchingClose =>
        CloseOnReverse && Status == AutoRuleStatus.Triggered && CloseWatchEndedAtUtc is null;

    /// <summary>Dành cho EF Core khi đọc từ database.</summary>
    private AutoOrderRule() { }

    /// <summary>Tạo quy tắc theo giá: mở lệnh khi mark price thỏa <paramref name="condition"/> so với <paramref name="triggerPrice"/>.</summary>
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
    /// <paramref name="closeOnReverse"/> = true: sau đó tự đóng vị thế khi chỉ báo phát tín hiệu ngược hướng.
    /// </summary>
    public static AutoOrderRule ForIndicator(string symbol, PositionSide side, string indicatorName,
        decimal margin, int leverage, decimal stopLossPercent, decimal? takeProfitPercent, DateTime createdAtUtc,
        bool closeOnReverse = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indicatorName);
        AutoOrderRule rule = new AutoOrderRule
        {
            TriggerType = RuleTriggerType.Indicator,
            IndicatorName = indicatorName,
            CloseOnReverse = closeOnReverse,
            CloseCommandId = closeOnReverse ? Guid.NewGuid() : null,
        };
        rule.Init(symbol, side, margin, leverage, stopLossPercent, takeProfitPercent, createdAtUtc);
        return rule;
    }

    /// <summary>Gán các giá trị chung cho mọi loại quy tắc và tạo <see cref="CommandId"/> mới.</summary>
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

    /// <summary>Ghi nhận đã kích hoạt và mở được vị thế <paramref name="positionId"/>. Chỉ gọi khi đang chờ.</summary>
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

    /// <summary>Ghi nhận điều kiện đã xảy ra nhưng lệnh bị từ chối kèm <paramref name="reason"/>; quy tắc không tự thử lại.</summary>
    public void MarkRejected(decimal markPrice, string reason, DateTime nowUtc, string? detail = null)
    {
        EnsureActive();
        TriggerDetail = detail;
        Status = AutoRuleStatus.Rejected;
        TriggeredAtUtc = nowUtc;
        TriggeredMarkPrice = markPrice;
        Note = reason;
    }

    /// <summary>Ngừng theo dõi đảo chiều kèm <paramref name="detail"/>. Chỉ gọi khi <see cref="IsWatchingClose"/>.</summary>
    public void EndCloseWatch(string detail, DateTime nowUtc)
    {
        if (!IsWatchingClose)
            throw new InvalidOperationException($"Quy tắc {Id} không theo dõi đảo chiều.");
        CloseWatchEndedAtUtc = nowUtc;
        CloseDetail = detail;
    }

    /// <summary>Hủy quy tắc đang chờ.</summary>
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

    /// <summary>Chặn mọi thay đổi trạng thái khi quy tắc không còn ở trạng thái chờ.</summary>
    private void EnsureActive()
    {
        if (Status != AutoRuleStatus.Active)
            throw new InvalidOperationException($"Quy tắc {Id} đã {Status}.");
    }
}
