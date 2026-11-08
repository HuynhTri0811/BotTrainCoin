namespace CryptoBot.Domain.MarketData;

/// <summary>Cả năm: mở cửa của tháng đầu tiên có dữ liệu, đóng cửa của tháng cuối cùng.</summary>
/// <param name="Complete">True khi đã có tháng 12 trọn vẹn.</param>
public sealed record YearChange(int Year, decimal Open, decimal Close, decimal ChangePercent, bool Complete);
