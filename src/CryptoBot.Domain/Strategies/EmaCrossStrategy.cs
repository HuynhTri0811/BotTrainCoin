using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Strategies;

public sealed record EmaCrossParameters(int FastPeriod = 9, int SlowPeriod = 21, int LookbackCandles = 300)
{
    public string? Validate()
    {
        if (FastPeriod < 1)
            return "FastPeriod phải >= 1.";
        if (SlowPeriod <= FastPeriod)
            return "SlowPeriod phải lớn hơn FastPeriod.";
        if (LookbackCandles < SlowPeriod + 1)
            return "LookbackCandles phải >= SlowPeriod + 1.";
        return null;
    }
}

/// <summary>
/// EMA crossover long-only trên nến đã đóng.
/// Quy tắc warm-up: luôn tính EMA trên đúng <see cref="EmaCrossParameters.LookbackCandles"/> nến
/// đã đóng gần nhất (kết thúc tại nến đánh giá), seed bằng SMA. Thiếu nến thì Hold.
/// Cửa sổ cố định giúp live, paper và backtest cho cùng giá trị EMA trên cùng dữ liệu.
/// </summary>
public sealed class EmaCrossStrategy : IStrategy
{
    private readonly EmaCrossParameters _p;

    public EmaCrossStrategy(EmaCrossParameters parameters)
    {
        if (parameters.Validate() is { } error)
            throw new ArgumentException(error, nameof(parameters));
        _p = parameters;
    }

    public int RequiredCandles => _p.LookbackCandles;

    public StrategyDecision Evaluate(StrategyContext context)
    {
        var candles = context.ClosedCandles;
        if (candles.Count == 0)
            return StrategyDecision.Hold("Không có nến.");
        if (candles.Any(c => !c.IsClosed))
            throw new ArgumentException("Chiến lược chỉ nhận nến đã đóng.", nameof(context));
        if (candles.Count < _p.LookbackCandles)
            return StrategyDecision.Hold($"Warm-up: có {candles.Count}/{_p.LookbackCandles} nến.");

        var indicators = ComputeLast(candles);
        var (fastPrev, slowPrev, fast, slow) = indicators;

        var crossedUp = fastPrev <= slowPrev && fast > slow;
        var crossedDown = fastPrev >= slowPrev && fast < slow;

        if (crossedUp)
        {
            if (context.HasPosition || context.HasPendingEntry)
                return StrategyDecision.Hold("Giao cắt lên nhưng đã có vị thế/pending entry.", indicators);
            return new StrategyDecision(SignalAction.Buy,
                $"EMA{_p.FastPeriod} cắt lên EMA{_p.SlowPeriod}.", indicators);
        }

        if (crossedDown)
        {
            if (!context.HasPosition)
                return StrategyDecision.Hold("Giao cắt xuống nhưng không có vị thế để bán.", indicators);
            return new StrategyDecision(SignalAction.Sell,
                $"EMA{_p.FastPeriod} cắt xuống EMA{_p.SlowPeriod}.", indicators);
        }

        return StrategyDecision.Hold("Không có giao cắt.", indicators);
    }

    /// <summary>
    /// Giá trị EMA fast/slow tại nến cuối của cửa sổ (dùng cả cho hiển thị),
    /// theo đúng quy tắc cửa sổ của <see cref="Evaluate"/>. Trả null nếu thiếu nến.
    /// </summary>
    public (decimal Fast, decimal Slow)? ComputeAt(IReadOnlyList<Candle> window)
    {
        if (window.Count < _p.LookbackCandles)
            return null;
        var v = ComputeLast(window);
        return (v.Fast, v.Slow);
    }

    private IndicatorValues ComputeLast(IReadOnlyList<Candle> candles)
    {
        var closes = new decimal[_p.LookbackCandles];
        var offset = candles.Count - _p.LookbackCandles;
        for (var i = 0; i < closes.Length; i++)
            closes[i] = candles[offset + i].Close;

        var fast = Ema.Calculate(closes, _p.FastPeriod);
        var slow = Ema.Calculate(closes, _p.SlowPeriod);
        var last = closes.Length - 1;

        return new IndicatorValues(fast[last - 1]!.Value, slow[last - 1]!.Value, fast[last]!.Value, slow[last]!.Value);
    }
}
