namespace CryptoBot.Domain.MarketData;

/// <summary>
/// Một nến OHLCV. Khóa tự nhiên: (Exchange, Symbol, Interval, OpenTimeUtc).
/// Mọi timestamp là UTC; mọi giá/lượng là decimal.
/// </summary>
public sealed class Candle
{
    public long Id { get; private set; }
    public string Exchange { get; private set; } = null!;
    public string Symbol { get; private set; } = null!;
    public string Interval { get; private set; } = null!;
    public DateTime OpenTimeUtc { get; private set; }
    public DateTime CloseTimeUtc { get; private set; }
    public decimal Open { get; private set; }
    public decimal High { get; private set; }
    public decimal Low { get; private set; }
    public decimal Close { get; private set; }
    public decimal Volume { get; private set; }
    public bool IsClosed { get; private set; }

    /// <summary>Thời điểm hệ thống nhận/cập nhật nến lần cuối, dùng phát hiện dữ liệu stale.</summary>
    public DateTime ReceivedAtUtc { get; private set; }

    private Candle() { }

    public Candle(
        string exchange,
        string symbol,
        CandleInterval interval,
        DateTime openTimeUtc,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume,
        bool isClosed,
        DateTime receivedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(interval);
        EnsureUtc(openTimeUtc, nameof(openTimeUtc));
        EnsureUtc(receivedAtUtc, nameof(receivedAtUtc));

        Exchange = exchange;
        Symbol = symbol;
        Interval = interval.Code;
        OpenTimeUtc = openTimeUtc;
        CloseTimeUtc = openTimeUtc + interval.Duration;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        IsClosed = isClosed;
        ReceivedAtUtc = receivedAtUtc;
    }

    /// <summary>
    /// Cập nhật giá trị từ bản mới hơn của cùng một nến. Nến đã đóng không bị ghi đè;
    /// trả về false nếu bản mới mâu thuẫn với nến đã đóng.
    /// </summary>
    public bool ApplyUpdate(Candle newer)
    {
        if (newer.Exchange != Exchange || newer.Symbol != Symbol ||
            newer.Interval != Interval || newer.OpenTimeUtc != OpenTimeUtc)
        {
            throw new InvalidOperationException("Chỉ cập nhật được từ cùng một nến.");
        }

        if (IsClosed)
        {
            return newer.Open == Open && newer.High == High && newer.Low == Low &&
                   newer.Close == Close && newer.Volume == Volume;
        }

        Open = newer.Open;
        High = newer.High;
        Low = newer.Low;
        Close = newer.Close;
        Volume = newer.Volume;
        IsClosed = newer.IsClosed;
        ReceivedAtUtc = newer.ReceivedAtUtc;
        return true;
    }

    /// <summary>Kiểm tra tính hợp lệ của OHLCV; trả về lý do nếu không hợp lệ.</summary>
    public string? Validate()
    {
        if (Open <= 0 || High <= 0 || Low <= 0 || Close <= 0)
            return "Giá phải dương.";
        if (Volume < 0)
            return "Volume âm.";
        if (High < Math.Max(Open, Close) || Low > Math.Min(Open, Close) || Low > High)
            return "High/Low không bao Open/Close.";
        return null;
    }

    private static void EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp phải là UTC.", name);
    }
}
