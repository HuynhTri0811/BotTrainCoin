namespace CryptoBot.Domain.MarketData;

/// <summary>Hướng nghiêng của dự báo "ngày tương tự".</summary>
public enum ForecastLean
{
    /// <summary>Xác suất tăng trong khoảng (40%, 60%): gần như tung đồng xu.</summary>
    Neutral,
    Up,
    Down,
}
