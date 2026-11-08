namespace CryptoBot.Domain.MarketData;

/// <summary>Giá một tháng: mở cửa của ngày đầu tiên có nến, đóng cửa của ngày cuối cùng có nến.</summary>
/// <param name="ChangePercent">(Close − Open) / Open × 100.</param>
/// <param name="Days">Số nến ngày trong tháng (sàn bỏ qua ngày không có giao dịch).</param>
/// <param name="Complete">False với tháng đang chạy: Close là giá gần nhất, chưa phải giá cuối tháng.</param>
public sealed record MonthChange(int Year, int Month, decimal Open, decimal Close, decimal ChangePercent, int Days,
    bool Complete);
