using CryptoBot.Domain.Futures;

namespace CryptoBot.Application.Futures;

/// <summary>
/// Kết quả hủy quy tắc. <paramref name="Rule"/> là trạng thái mới nhất (null khi không tìm thấy); khi không hủy được
/// thì cho biết quy tắc đã kết thúc thế nào, ví dụ đã kích hoạt và mở vị thế.
/// </summary>
public sealed record CancelRuleResult(bool Cancelled, AutoOrderRule? Rule);
