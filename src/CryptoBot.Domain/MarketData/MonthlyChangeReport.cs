namespace CryptoBot.Domain.MarketData;

/// <summary>Giá một tháng: mở cửa của ngày đầu tiên có nến, đóng cửa của ngày cuối cùng có nến.</summary>
/// <param name="ChangePercent">(Close − Open) / Open × 100.</param>
/// <param name="Days">Số nến ngày trong tháng (sàn bỏ qua ngày không có giao dịch).</param>
/// <param name="Complete">False với tháng đang chạy: Close là giá gần nhất, chưa phải giá cuối tháng.</param>
public sealed record MonthChange(int Year, int Month, decimal Open, decimal Close, decimal ChangePercent, int Days,
    bool Complete);

/// <summary>Cả năm: mở cửa của tháng đầu tiên có dữ liệu, đóng cửa của tháng cuối cùng.</summary>
/// <param name="Complete">True khi đã có tháng 12 trọn vẹn.</param>
public sealed record YearChange(int Year, decimal Open, decimal Close, decimal ChangePercent, bool Complete);

/// <summary>Cùng một tháng (ví dụ tháng 3) qua các năm, chỉ tính tháng trọn vẹn.</summary>
/// <param name="Years">Số năm có tháng này.</param>
/// <param name="UpPercent">Tỷ lệ năm tháng này đóng cao hơn mở (%).</param>
public sealed record MonthOfYearStats(int Month, int Years, decimal AverageChangePercent, decimal BestChangePercent,
    decimal WorstChangePercent, int UpCount, decimal UpPercent);

/// <summary>
/// Chênh lệch giá mở cửa → đóng cửa theo từng tháng của từng năm, từ nến ngày. Dùng để so các tháng với nhau
/// (tính mùa vụ); không dự đoán tháng tới.
/// </summary>
public sealed record MonthlyChangeReport(
    IReadOnlyList<MonthChange> Months,
    IReadOnlyList<YearChange> Years,
    IReadOnlyList<MonthOfYearStats> MonthStats)
{
    /// <summary>Nến 1d tăng dần theo OpenTime; nến cuối có thể đang chạy.</summary>
    public static MonthlyChangeReport Build(IReadOnlyList<Candle> dailyCandles)
    {
        if (dailyCandles.Any(candle => candle.Interval != CandleInterval.D1.Code))
            throw new ArgumentException("Chỉ nhận nến ngày (1d).", nameof(dailyCandles));

        List<IGrouping<(int Year, int Month), Candle>> groups = dailyCandles
            .OrderBy(candle => candle.OpenTimeUtc)
            .GroupBy(candle => (candle.OpenTimeUtc.Year, candle.OpenTimeUtc.Month))
            .ToList();

        List<MonthChange> months = new(groups.Count);
        for (int index = 0; index < groups.Count; index++)
        {
            Candle first = groups[index].First();
            Candle last = groups[index].Last();
            DateTime nextMonth = new DateTime(groups[index].Key.Year, groups[index].Key.Month, 1, 0, 0, 0,
                DateTimeKind.Utc)
                .AddMonths(1);
            // Trọn vẹn khi đã có tháng sau, hoặc nến ngày cuối tháng đã đóng.
            bool complete = index < groups.Count - 1 || (last.IsClosed && last.CloseTimeUtc >= nextMonth);
            months.Add(new MonthChange(groups[index].Key.Year, groups[index].Key.Month, first.Open, last.Close,
                ChangePercent(first.Open, last.Close), groups[index].Count(), complete));
        }

        List<YearChange> years = months
            .GroupBy(change => change.Year)
            .Select(group => new YearChange(group.Key, group.First().Open, group.Last().Close,
                ChangePercent(group.First().Open, group.Last().Close), group.Last().Month == 12
                    && group.Last().Complete))
            .ToList();

        List<MonthOfYearStats> stats = months
            .Where(change => change.Complete)
            .GroupBy(change => change.Month)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                int up = group.Count(change => change.ChangePercent > 0);
                return new MonthOfYearStats(group.Key, group.Count(), group.Average(change => change.ChangePercent),
                    group.Max(change => change.ChangePercent), group.Min(change => change.ChangePercent), up,
                        (decimal)up / group.Count() * 100);
            })
            .ToList();

        return new MonthlyChangeReport(months, years, stats);
    }

    private static decimal ChangePercent(decimal open, decimal close) =>
        open == 0 ? 0 : (close - open) / open * 100;
}
