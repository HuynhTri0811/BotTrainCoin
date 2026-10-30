namespace CryptoBot.Domain.MarketData;

/// <summary>Hướng nghiêng của dự báo "ngày tương tự".</summary>
public enum ForecastLean
{
    /// <summary>Xác suất tăng trong khoảng (40%, 60%): gần như tung đồng xu.</summary>
    Neutral,

    /// <summary>Ít nhất 60% số ngày tương tự tăng sau đó.</summary>
    Up,

    /// <summary>Nhiều nhất 40% số ngày tương tự tăng sau đó.</summary>
    Down,
}
