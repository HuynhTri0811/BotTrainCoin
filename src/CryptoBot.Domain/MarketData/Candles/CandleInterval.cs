using System.Diagnostics.CodeAnalysis;

namespace CryptoBot.Domain.MarketData;

/// <summary>Khung nến được hỗ trợ. Code trùng với mã interval của Binance.</summary>
public sealed record CandleInterval
{
    public static readonly CandleInterval M15 = new("15m", TimeSpan.FromMinutes(15));
    public static readonly CandleInterval H1 = new("1h", TimeSpan.FromHours(1));
    public static readonly CandleInterval D1 = new("1d", TimeSpan.FromDays(1));

    private static readonly IReadOnlyDictionary<string, CandleInterval> All =
        new Dictionary<string, CandleInterval>(StringComparer.Ordinal)
        {
            [M15.Code] = M15,
            [H1.Code] = H1,
            [D1.Code] = D1,
        };

    public string Code { get; }
    public TimeSpan Duration { get; }

    private CandleInterval(string code, TimeSpan duration)
    {
        Code = code;
        Duration = duration;
    }

    public static bool TryParse(string? code, [MaybeNullWhen(false)] out CandleInterval interval)
    {
        interval = null;
        return code is not null && All.TryGetValue(code, out interval);
    }

    public static CandleInterval Parse(string code) =>
        TryParse(code, out CandleInterval? interval)
            ? interval
            : throw new ArgumentException($"Interval không được hỗ trợ: '{code}'.", nameof(code));

    /// <summary>
    /// OpenTime của nến chứa thời điểm <paramref name="timeUtc"/>: làm tròn xuống bội số của <see cref="Duration"/>
    /// tính từ 0001-01-01 (trùng mốc 00:00 UTC, nên nến 1h bắt đầu đúng giờ, nến 1d bắt đầu lúc 00:00).
    /// </summary>
    public DateTime OpenTimeAt(DateTime timeUtc) =>
        new(timeUtc.Ticks - timeUtc.Ticks % Duration.Ticks, DateTimeKind.Utc);

    public override string ToString() => Code;
}
