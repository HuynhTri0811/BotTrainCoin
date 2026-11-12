using CryptoBot.Domain.Futures;
using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Backtesting;

/// <summary>
/// Chỉ báo tự viết (MyRsi, FlowRsi…) làm nguồn tín hiệu: gọi đúng <see cref="ICustomIndicator.Compute"/> như lúc
/// quy tắc tự động đọc chỉ báo, <see cref="IndicatorSignal.Long"/> → Long, <see cref="IndicatorSignal.Short"/> → Short.
/// </summary>
public sealed class IndicatorSignalSource(ICustomIndicator indicator) : IBacktestSignalSource
{
    public int RequiredCandles => indicator.RequiredCandles;

    public BacktestSignalReading Read(IReadOnlyList<Candle> window)
    {
        IndicatorResult result = indicator.Compute(window);
        return result.Signal switch
        {
            IndicatorSignal.Long => new BacktestSignalReading(PositionSide.Long, result.Reason),
            IndicatorSignal.Short => new BacktestSignalReading(PositionSide.Short, result.Reason),
            _ => BacktestSignalReading.None,
        };
    }
}
