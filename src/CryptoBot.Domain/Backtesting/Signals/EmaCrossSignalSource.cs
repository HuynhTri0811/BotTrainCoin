using CryptoBot.Domain.Futures;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Backtesting;

/// <summary>EMA cross hai chiều: EMA nhanh cắt lên EMA chậm → Long, cắt xuống → Short.</summary>
public sealed class EmaCrossSignalSource(EmaCrossParameters parameters) : IBacktestSignalSource
{
    /// <summary>Chiến lược dùng để tính EMA (cũng để vẽ đường EMA trên chart).</summary>
    public EmaCrossStrategy Strategy { get; } = new(parameters);

    public int RequiredCandles => Strategy.RequiredCandles;

    public BacktestSignalReading Read(IReadOnlyList<Candle> window) => Strategy.CrossAt(window) switch
    {
        EmaCrossDirection.Up => new BacktestSignalReading(PositionSide.Long,
            $"EMA{parameters.FastPeriod} cắt lên EMA{parameters.SlowPeriod}."),
        EmaCrossDirection.Down => new BacktestSignalReading(PositionSide.Short,
            $"EMA{parameters.FastPeriod} cắt xuống EMA{parameters.SlowPeriod}."),
        _ => BacktestSignalReading.None,
    };
}
