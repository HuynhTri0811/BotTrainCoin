namespace CryptoBot.Domain.MarketData;

public enum CandleIssueKind
{
    InvalidValues,
    Duplicate,
    OutOfOrder,
    Gap,
}

public sealed record CandleIssue(CandleIssueKind Kind, DateTime OpenTimeUtc, string Detail);

/// <summary>Phát hiện nến trùng, sai thứ tự, thiếu và giá trị không hợp lệ trong một chuỗi nến.</summary>
public static class CandleSeriesValidator
{
    public static IReadOnlyList<CandleIssue> Validate(IReadOnlyList<Candle> candles, CandleInterval interval)
    {
        List<CandleIssue> issues = new List<CandleIssue>();

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
}
