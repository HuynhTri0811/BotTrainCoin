namespace CryptoBot.Domain.MarketData;

/// <summary>
/// Một nến OHLCV. Khóa tự nhiên: (Exchange, Symbol, Interval, OpenTimeUtc).
/// Mọi timestamp là UTC; mọi giá/lượng là decimal.
/// </summary>
public sealed class Candle
{
    /// <summary>Khóa chính do database cấp.</summary>
    public long Id { get; private set; }

    /// <summary>Sàn lấy dữ liệu, ví dụ "Binance".</summary>
    public string Exchange { get; private set; } = null!;

    /// <summary>Cặp giao dịch, ví dụ "BTCUSDT".</summary>
    public string Symbol { get; private set; } = null!;

    /// <summary>Mã khung nến, ví dụ "15m" hoặc "1h".</summary>
    public string Interval { get; private set; } = null!;

    /// <summary>Thời điểm mở nến (UTC); cùng Exchange/Symbol/Interval tạo thành khóa duy nhất của nến.</summary>
    public DateTime OpenTimeUtc { get; private set; }

    /// <summary>Thời điểm kết thúc nến (UTC) = OpenTimeUtc + độ dài khung.</summary>
    public DateTime CloseTimeUtc { get; private set; }

    /// <summary>Giá mở cửa.</summary>
    public decimal Open { get; private set; }

    /// <summary>Giá cao nhất trong nến.</summary>
    public decimal High { get; private set; }

    /// <summary>Giá thấp nhất trong nến.</summary>
    public decimal Low { get; private set; }

    /// <summary>Giá đóng cửa (với nến đang chạy là giá gần nhất).</summary>
    public decimal Close { get; private set; }

    /// <summary>Khối lượng giao dịch (đơn vị coin).</summary>
    public decimal Volume { get; private set; }

    /// <summary>Khối lượng giao dịch quy ra đồng định giá (USDT với cặp ...USDT); null với nến lưu trước khi có cột này.</summary>
    public decimal? QuoteVolume { get; private set; }

    /// <summary>Số giao dịch (lệnh khớp) trong nến; null với nến lưu trước khi có cột này.</summary>
    public long? TradeCount { get; private set; }

    /// <summary>True khi nến đã kết thúc theo giờ server của sàn; chiến lược chỉ dùng nến đã đóng.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Thời điểm hệ thống nhận/cập nhật nến lần cuối, dùng phát hiện dữ liệu stale.</summary>
    public DateTime ReceivedAtUtc { get; private set; }

    /// <summary>Dành cho EF Core khi đọc từ database.</summary>
    private Candle() { }

    /// <summary>Tạo nến mới. Mọi timestamp phải là UTC; CloseTimeUtc được tính từ khung nến.</summary>
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
        DateTime receivedAtUtc,
        decimal? quoteVolume = null,
        long? tradeCount = null)
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
        QuoteVolume = quoteVolume;
        TradeCount = tradeCount;
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
            bool same = newer.Open == Open && newer.High == High && newer.Low == Low &&
                        newer.Close == Close && newer.Volume == Volume;
            // Nến đã đóng lưu trước khi có số giao dịch: bổ sung khi sàn trả lại cùng nến.
            if (same)
            {
                QuoteVolume ??= newer.QuoteVolume;
                TradeCount ??= newer.TradeCount;
            }

            return same;
        }

        Open = newer.Open;
        High = newer.High;
        Low = newer.Low;
        Close = newer.Close;
        Volume = newer.Volume;
        QuoteVolume = newer.QuoteVolume;
        TradeCount = newer.TradeCount;
        IsClosed = newer.IsClosed;
        ReceivedAtUtc = newer.ReceivedAtUtc;
        return true;
    }

    /// <summary>Kiểm tra tính hợp lệ của OHLCV; trả về lý do nếu không hợp lệ.</summary>
    public string? Validate()
    {
        if (Open <= 0 || High <= 0 || Low <= 0 || Close <= 0)
            return "Giá phải dương.";
        if (Volume < 0 || QuoteVolume < 0)
            return "Volume âm.";
        if (TradeCount < 0)
            return "Số giao dịch âm.";
        if (High < Math.Max(Open, Close) || Low > Math.Min(Open, Close) || Low > High)
            return "High/Low không bao Open/Close.";
        return null;
    }

    /// <summary>Từ chối DateTime không phải UTC để tránh lệch giờ khi lưu và so sánh.</summary>
    private static void EnsureUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp phải là UTC.", name);
    }
}
