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

    /// <summary>Kiểm tra đầu vào → tính EMA → quyết định theo giao cắt và vị thế hiện có.</summary>
    public StrategyDecision Evaluate(StrategyContext context)
    {
        if (CheckInput(context.ClosedCandles) is { } hold)
            return hold;

        var indicators = ComputeLast(LastCloses(context.ClosedCandles));
        return Decide(indicators, context);
    }

    /// <summary>
    /// Giá trị EMA fast/slow tại nến cuối của cửa sổ (dùng cả cho hiển thị),
    /// theo đúng quy tắc cửa sổ của <see cref="Evaluate"/>. Trả null nếu thiếu nến.
    /// </summary>
    public (decimal Fast, decimal Slow)? ComputeAt(IReadOnlyList<Candle> window) =>
        window.Count < _p.LookbackCandles ? null : ComputeAt(LastCloses(window));

    /// <summary>
    /// Như <see cref="ComputeAt(IReadOnlyList{Candle})"/> nhưng nhận thẳng giá đóng cửa (đoạn kết thúc tại nến
    /// cần tính), để vòng lặp vẽ chart không phải sao chép cửa sổ cho từng nến.
    /// </summary>
    public (decimal Fast, decimal Slow)? ComputeAt(ReadOnlySpan<decimal> closes)
    {
        if (closes.Length < _p.LookbackCandles)
            return null;
        var v = ComputeLast(closes[^_p.LookbackCandles..]);
        return (v.Fast, v.Slow);
    }

    // ── Các bước nhỏ ───────────────────────────────────────────────────────────

    /// <summary>Trả về Hold nếu chưa đủ dữ liệu; ném lỗi nếu nhận nến chưa đóng.</summary>
    private StrategyDecision? CheckInput(IReadOnlyList<Candle> candles)
    {
        if (candles.Count == 0)
            return StrategyDecision.Hold("Không có nến.");
        for (var i = 0; i < candles.Count; i++)
            if (!candles[i].IsClosed)
                throw new ArgumentException("Chiến lược chỉ nhận nến đã đóng.", nameof(candles));
        if (candles.Count < _p.LookbackCandles)
            return StrategyDecision.Hold($"Warm-up: có {candles.Count}/{_p.LookbackCandles} nến.");
        return null;
    }

    /// <summary>Giá đóng cửa của đúng <see cref="EmaCrossParameters.LookbackCandles"/> nến cuối.</summary>
    private decimal[] LastCloses(IReadOnlyList<Candle> candles)
    {
        var closes = new decimal[_p.LookbackCandles];
        var offset = candles.Count - _p.LookbackCandles;
        for (var i = 0; i < closes.Length; i++)
            closes[i] = candles[offset + i].Close;
        return closes;
    }

    /// <summary>EMA fast/slow tại nến trước và nến cuối của cửa sổ.</summary>
    private IndicatorValues ComputeLast(ReadOnlySpan<decimal> closes)
    {
        var (fastPrev, fast) = Ema.LastTwo(closes, _p.FastPeriod);
        var (slowPrev, slow) = Ema.LastTwo(closes, _p.SlowPeriod);
        return new IndicatorValues(fastPrev, slowPrev, fast, slow);
    }

    private StrategyDecision Decide(IndicatorValues v, StrategyContext context)
    {
        if (Crossover.Up(v.FastPrev, v.SlowPrev, v.Fast, v.Slow))
            return OnCrossUp(v, context);
        if (Crossover.Down(v.FastPrev, v.SlowPrev, v.Fast, v.Slow))
            return OnCrossDown(v, context);
        return StrategyDecision.Hold("Không có giao cắt.", v);
    }

    /// <summary>Giao cắt lên → BUY, trừ khi đã có vị thế hoặc lệnh vào đang chờ.</summary>
    private StrategyDecision OnCrossUp(IndicatorValues v, StrategyContext context) =>
        context.HasPosition || context.HasPendingEntry
            ? StrategyDecision.Hold("Giao cắt lên nhưng đã có vị thế/pending entry.", v)
            : new StrategyDecision(SignalAction.Buy, $"EMA{_p.FastPeriod} cắt lên EMA{_p.SlowPeriod}.", v);

    /// <summary>Giao cắt xuống → SELL, chỉ khi đang giữ vị thế (long-only).</summary>
    private StrategyDecision OnCrossDown(IndicatorValues v, StrategyContext context) =>
        context.HasPosition
            ? new StrategyDecision(SignalAction.Sell, $"EMA{_p.FastPeriod} cắt xuống EMA{_p.SlowPeriod}.", v)
            : StrategyDecision.Hold("Giao cắt xuống nhưng không có vị thế để bán.", v);
}
