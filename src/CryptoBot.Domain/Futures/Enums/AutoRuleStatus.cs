namespace CryptoBot.Domain.Futures;

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
