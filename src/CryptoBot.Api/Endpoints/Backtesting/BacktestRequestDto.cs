using CryptoBot.Application.Backtesting;
using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Thân request chạy backtest. Số tiền có thể gửi dạng chuỗi. Fast/Slow/Lookback để null thì dùng tham số của
/// chiến lược; TP/SL để null thì không dùng. Sizing: <c>PercentOfEquity</c> hoặc <c>FixedAmount</c>.
/// </summary>
/// <remarks>
/// Mode: <c>Spot</c> (mặc định; chỉ Long, như bot EMA) hoặc <c>Futures</c>. Với Futures: Leverage bắt buộc,
/// Direction <c>LongOnly</c>/<c>ShortOnly</c>/<c>Both</c> (mặc định Both), CloseOnReverse mặc định true, trượt giá và
/// ký quỹ duy trì mặc định 0 (dashboard gửi mức của Paper Futures). SignalType null/<c>EmaCross</c> = EMA cross, hoặc
/// loại chỉ báo tự viết (chỉ Futures) với SignalParameters.
/// </remarks>
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
    decimal FeePercent,
    string? Mode = null,
    int? Leverage = null,
    string? Direction = null,
    bool? CloseOnReverse = null,
    decimal? SlippagePercent = null,
    decimal? MaintenanceMarginPercent = null,
    string? SignalType = null,
    Dictionary<string, string?>? SignalParameters = null)
{
    /// <summary>Đổi sang request của Application; trả về lỗi nếu chữ của enum hoặc Futures thiếu đòn bẩy.</summary>
    /// <param name="strategyId">Instance lấy từ đường dẫn.</param>
    public (BacktestRequest? Request, string? Error) ToRequest(int strategyId)
    {
        if (!QueryEnum.TryParse<PositionSizing>(Sizing, out var parsedSizing) || parsedSizing is not { } sizing)
            return (null, QueryEnum.Error<PositionSizing>(Sizing ?? ""));
        if (!QueryEnum.TryParse<BacktestMode>(Mode, out var mode))
            return (null, QueryEnum.Error<BacktestMode>(Mode!));

        FuturesBacktestSettings? futures = null;
        if (mode == BacktestMode.Futures)
        {
            if (Leverage is not { } leverage)
                return (null, "Backtest Futures cần đòn bẩy (Leverage).");
            if (!QueryEnum.TryParse<BacktestDirection>(Direction, out var direction))
                return (null, QueryEnum.Error<BacktestDirection>(Direction!));
            futures = new FuturesBacktestSettings(leverage, direction ?? BacktestDirection.Both, CloseOnReverse ?? true,
                SlippagePercent ?? 0m, MaintenanceMarginPercent ?? 0m);
        }

        return (new BacktestRequest(strategyId, FromUtc?.UtcDateTime, ToUtc?.UtcDateTime,
            new EmaCrossOverrides(FastPeriod, SlowPeriod, LookbackCandles),
            new BacktestSettings(InitialCapital, sizing, SizeValue, TakeProfitPercent, StopLossPercent, FeePercent,
                futures),
            SignalType, SignalParameters), null);
    }
}
