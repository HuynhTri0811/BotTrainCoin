namespace CryptoBot.Domain.MarketData;

/// <summary>Loại lỗi tìm thấy trong một chuỗi nến.</summary>
public enum CandleIssueKind
{
    /// <summary>OHLCV không hợp lệ, xem <see cref="Candle.Validate"/>.</summary>
    InvalidValues,

    /// <summary>Hai nến liền nhau có cùng OpenTime.</summary>
    Duplicate,

    /// <summary>Nến đứng sau một nến mở muộn hơn.</summary>
    OutOfOrder,

    /// <summary>Thiếu ít nhất một nến giữa hai nến liền nhau.</summary>
    Gap,
}

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

        for (int i = 0; i < candles.Count; i++)
        {
            Candle c = candles[i];
            if (c.Validate() is { } reason)
                issues.Add(new CandleIssue(CandleIssueKind.InvalidValues, c.OpenTimeUtc, reason));

            if (i == 0)
                continue;

            DateTime prev = candles[i - 1].OpenTimeUtc;
            TimeSpan diff = c.OpenTimeUtc - prev;
            if (diff == TimeSpan.Zero)
                issues.Add(new CandleIssue(CandleIssueKind.Duplicate, c.OpenTimeUtc, "Trùng OpenTime."));
            else if (diff < TimeSpan.Zero)
                issues.Add(new CandleIssue(CandleIssueKind.OutOfOrder, c.OpenTimeUtc, $"Đứng sau {prev:O}."));
            else if (diff != interval.Duration)
            {
                int missing = (int)(diff.Ticks / interval.Duration.Ticks) - 1;
                issues.Add(new CandleIssue(CandleIssueKind.Gap, c.OpenTimeUtc,
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
        HashSet<DateTime> rejected = issues.Where(i => i.RejectsCandle).Select(i => i.OpenTimeUtc).ToHashSet();
        return candles.Where(c => !rejected.Contains(c.OpenTimeUtc)).ToList();
    }
}
