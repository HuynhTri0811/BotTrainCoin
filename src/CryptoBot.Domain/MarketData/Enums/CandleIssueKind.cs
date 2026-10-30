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
