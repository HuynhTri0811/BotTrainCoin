using CryptoBot.Application.Backtesting;
using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Thân request chạy backtest. Số tiền có thể gửi dạng chuỗi. Fast/Slow/Lookback để null thì dùng tham số của
/// chiến lược; TP/SL để null thì không dùng. Sizing: <c>PercentOfEquity</c> hoặc <c>FixedAmount</c>.
/// </summary>
public sealed record BacktestRequestDto(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int? FastPeriod,
    int? SlowPeriod,
    int? LookbackCandles,
    decimal InitialCapital,
    string Sizing,
    decimal SizeValue,
    decimal? TakeProfitPercent,
    decimal? StopLossPercent,
    decimal FeePercent)
{
    /// <summary>Đổi sang request của Application; null nếu <see cref="Sizing"/> không hợp lệ.</summary>
    /// <param name="strategyId">Instance lấy từ đường dẫn.</param>
    public BacktestRequest? ToRequest(int strategyId)
    {
        if (!Enum.TryParse<PositionSizing>(Sizing, ignoreCase: true, out var sizing) || !Enum.IsDefined(sizing)
            || int.TryParse(Sizing, out _))
            return null;

        return new BacktestRequest(strategyId, FromUtc?.UtcDateTime, ToUtc?.UtcDateTime,
            new EmaCrossOverrides(FastPeriod, SlowPeriod, LookbackCandles),
            new BacktestSettings(InitialCapital, sizing, SizeValue, TakeProfitPercent, StopLossPercent, FeePercent));
    }
}
