namespace CryptoBot.Domain.MarketData;

/// <summary>Khung nến được hỗ trợ. Code trùng với mã interval của Binance.</summary>
public sealed record CandleInterval
{
    public static readonly CandleInterval M15 = new("15m", TimeSpan.FromMinutes(15));
    public static readonly CandleInterval H1 = new("1h", TimeSpan.FromHours(1));

    private static readonly IReadOnlyDictionary<string, CandleInterval> All =
        new Dictionary<string, CandleInterval>(StringComparer.Ordinal)
        {
            [M15.Code] = M15,
            [H1.Code] = H1,
        };

    public string Code { get; }
    public TimeSpan Duration { get; }

    private CandleInterval(string code, TimeSpan duration)
    {
        Code = code;
        Duration = duration;
    }

    public static bool TryParse(string? code, out CandleInterval interval)
    {
        if (code is not null && All.TryGetValue(code, out CandleInterval found))
        {
            interval = found;
            return true;
        }

        interval = null!;
        return false;
    }

    public static CandleInterval Parse(string code) =>
        TryParse(code, out CandleInterval interval)
            ? interval
            : throw new ArgumentException($"Interval không được hỗ trợ: '{code}'.", nameof(code));

    public override string ToString() => Code;
}
