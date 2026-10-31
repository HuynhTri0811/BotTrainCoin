namespace CryptoBot.Domain.MarketData;

/// <summary>Một lỗi trong chuỗi nến, gắn với nến có OpenTime <paramref name="OpenTimeUtc"/>.</summary>
public sealed record CandleIssue(CandleIssueKind Kind, DateTime OpenTimeUtc, string Detail)
{
    /// <summary>
    /// True khi nến mang lỗi này không được lưu (giá trị sai, trùng, sai thứ tự). Gap chỉ được ghi nhận:
    /// nến sau khoảng thiếu vẫn đúng.
    /// </summary>
    public bool RejectsCandle => Kind is not CandleIssueKind.Gap;
}

/// <summary>Phát hiện nến trùng, sai thứ tự, thiếu và giá trị không hợp lệ trong một chuỗi nến.</summary>
public static class CandleSeriesValidator
{
    /// <summary>Chuỗi nến phải tăng dần theo OpenTime, cách nhau đúng một <paramref name="interval"/>.</summary>
    public static IReadOnlyList<CandleIssue> Validate(IReadOnlyList<Candle> candles, CandleInterval interval)
    {
        List<CandleIssue> issues = [];

        for (int index = 0; index < candles.Count; index++)
        {
            Candle candle = candles[index];
            if (candle.Validate() is { } reason)
                issues.Add(new CandleIssue(CandleIssueKind.InvalidValues, candle.OpenTimeUtc, reason));

            if (index == 0)
                continue;

            DateTime prev = candles[index - 1].OpenTimeUtc;
            TimeSpan diff = candle.OpenTimeUtc - prev;
            if (diff == TimeSpan.Zero)
                issues.Add(new CandleIssue(CandleIssueKind.Duplicate, candle.OpenTimeUtc, "Trùng OpenTime."));
            else if (diff < TimeSpan.Zero)
                issues.Add(new CandleIssue(CandleIssueKind.OutOfOrder, candle.OpenTimeUtc, $"Đứng sau {prev:O}."));
            else if (diff != interval.Duration)
            {
                int missing = (int)(diff.Ticks / interval.Duration.Ticks) - 1;
                issues.Add(new CandleIssue(CandleIssueKind.Gap, candle.OpenTimeUtc,
                    $"Thiếu khoảng {missing} nến sau {prev:O}."));
            }
        }

        return issues;
    }

    /// <summary>
    /// Bỏ mọi nến có OpenTime trùng với một lỗi <see cref="CandleIssue.RejectsCandle"/>, giữ nguyên thứ tự còn lại.
    /// Không sửa dữ liệu âm thầm: nến hỏng bị loại chứ không được chỉnh lại.
    /// </summary>
    public static List<Candle> WithoutRejected(IReadOnlyList<Candle> candles, IReadOnlyList<CandleIssue> issues)
    {
        HashSet<DateTime> rejected = issues.Where(issue => issue.RejectsCandle).Select(issue => issue.OpenTimeUtc)
            .ToHashSet();
        return candles.Where(candle => !rejected.Contains(candle.OpenTimeUtc)).ToList();
    }
}
