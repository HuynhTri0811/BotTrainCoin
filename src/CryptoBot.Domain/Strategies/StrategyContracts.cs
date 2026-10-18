using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Strategies;

public enum SignalAction
{
    Hold = 0,
    Buy = 1,
    Sell = 2,
}

/// <summary>
/// Đầu vào cho một lần đánh giá. <see cref="ClosedCandles"/> là cửa sổ nến đã đóng,
/// sắp tăng dần theo thời gian, phần tử cuối là nến đang được đánh giá.
/// </summary>
public sealed record StrategyContext(
    IReadOnlyList<Candle> ClosedCandles,
    bool HasPosition,
    bool HasPendingEntry,
    int ConfigVersion);

public sealed record IndicatorValues(decimal FastPrev, decimal SlowPrev, decimal Fast, decimal Slow);

public sealed record StrategyDecision(SignalAction Action, string Reason, IndicatorValues? Indicators)
{
    public static StrategyDecision Hold(string reason, IndicatorValues? indicators = null) =>
        new(SignalAction.Hold, reason, indicators);
}

/// <summary>Biến dữ liệu đầu vào thành quyết định. Không gọi API sàn, không đọc đồng hồ hệ thống.</summary>
public interface IStrategy
{
    /// <summary>Số nến đã đóng tối thiểu cần có trong cửa sổ trước khi được ra quyết định.</summary>
    int RequiredCandles { get; }

    StrategyDecision Evaluate(StrategyContext context);
}
