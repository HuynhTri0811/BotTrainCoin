using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Backtesting;

/// <summary>
/// Nguồn tín hiệu Long/Short cho backtest Futures: EMA cross (<see cref="EmaCrossSignalSource"/>) hoặc chỉ báo tự
/// viết (<see cref="IndicatorSignalSource"/>). Giống chiến lược, chỉ nhận cửa sổ nến đã đóng và không giữ trạng thái.
/// </summary>
public interface IBacktestSignalSource
{
    /// <summary>Số nến của cửa sổ đưa vào <see cref="Read"/>.</summary>
    int RequiredCandles { get; }

    /// <summary>Tín hiệu tại nến cuối của <paramref name="window"/> (đúng <see cref="RequiredCandles"/> nến).</summary>
    BacktestSignalReading Read(IReadOnlyList<Candle> window);
}
