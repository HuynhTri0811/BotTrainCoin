using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Strategies;

/// <summary>
/// Đầu vào cho một lần đánh giá. <see cref="ClosedCandles"/> là cửa sổ nến đã đóng,
/// sắp tăng dần theo thời gian, phần tử cuối là nến đang được đánh giá.
/// </summary>
public sealed record StrategyContext(
    IReadOnlyList<Candle> ClosedCandles,
    bool HasPosition,
    bool HasPendingEntry,
    int ConfigVersion);
