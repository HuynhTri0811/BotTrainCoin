namespace CryptoBot.Domain.MarketData;

/// <summary>Cùng một tháng (ví dụ tháng 3) qua các năm, chỉ tính tháng trọn vẹn.</summary>
/// <param name="Years">Số năm có tháng này.</param>
/// <param name="UpPercent">Tỷ lệ năm tháng này đóng cao hơn mở (%).</param>
public sealed record MonthOfYearStats(int Month, int Years, decimal AverageChangePercent, decimal BestChangePercent,
    decimal WorstChangePercent, int UpCount, decimal UpPercent);
