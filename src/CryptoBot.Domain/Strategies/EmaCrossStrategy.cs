using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Strategies;

/// <summary>
/// Tham số của chiến lược EMA cross (đọc từ appsettings.json → "Strategies" → "Parameters").
/// </summary>
/// <param name="FastPeriod">Số nến của EMA nhanh (mặc định 9): bám giá sát, đổi hướng sớm.</param>
/// <param name="SlowPeriod">Số nến của EMA chậm (mặc định 21): mượt hơn, thể hiện xu hướng chung.</param>
/// <param name="LookbackCandles">Số nến dùng để tính mỗi lần (mặc định 300), xem giải thích ở class bên dưới.</param>
public sealed record EmaCrossParameters(int FastPeriod = 9, int SlowPeriod = 21, int LookbackCandles = 300)
{
    /// <summary>Kiểm tra tham số; trả về lý do nếu sai, null nếu hợp lệ.</summary>
    public string? Validate()
    {
        if (FastPeriod < 1)
            return "FastPeriod phải >= 1.";
        // EMA nhanh phải ngắn hơn EMA chậm, nếu không thì "nhanh" và "chậm" bị đảo nghĩa.
        if (SlowPeriod <= FastPeriod)
            return "SlowPeriod phải lớn hơn FastPeriod.";
        // Cần thêm ít nhất 1 nến sau khi EMA chậm có giá trị đầu tiên, để có cả "nến trước" lẫn "nến này".
        if (LookbackCandles < SlowPeriod + 1)
            return "LookbackCandles phải >= SlowPeriod + 1.";
        return null;
    }
}

/// <summary>
/// HÀM DỰ ĐOÁN số 1: chiến lược EMA cross, chỉ mua (long-only), chạy trên nến đã đóng.
///
/// Ý tưởng:
///   - EMA (đường trung bình động hàm mũ) là giá trung bình nhưng ưu tiên các nến gần đây.
///   - EMA9 (nhanh) phản ứng nhanh với giá; EMA21 (chậm) thể hiện xu hướng.
///   - EMA9 cắt LÊN EMA21  → giá đang tăng mạnh hơn xu hướng → phát BUY.
///   - EMA9 cắt XUỐNG EMA21 → giá đang yếu đi                   → phát SELL (chỉ khi đang giữ).
///
/// Ví dụ: nến trước EMA9 = 81,500 &lt; EMA21 = 81,520; nến này EMA9 = 81,560 &gt; EMA21 = 81,530
/// → EMA9 vừa vượt lên trên EMA21 → BUY.
///
/// Quy tắc cửa sổ cố định (warm-up): luôn tính EMA trên đúng <see cref="EmaCrossParameters.LookbackCandles"/>
/// nến đã đóng gần nhất (kết thúc tại nến đánh giá), seed bằng SMA. Thiếu nến thì Hold.
/// Vì EMA phụ thuộc điểm bắt đầu tính, cố định cửa sổ giúp live, paper, chart và backtest
/// luôn cho cùng giá trị EMA trên cùng dữ liệu.
/// </summary>
public sealed class EmaCrossStrategy : IStrategy
{
    private readonly EmaCrossParameters _p;

    /// <summary>Tạo chiến lược; ném lỗi ngay nếu tham số sai để không chạy với cấu hình hỏng.</summary>
    public EmaCrossStrategy(EmaCrossParameters parameters)
    {
        if (parameters.Validate() is { } error)
            throw new ArgumentException(error, nameof(parameters));
        _p = parameters;
    }

    /// <summary>Số nến đã đóng phải có trước khi chiến lược được phép ra quyết định.</summary>
    public int RequiredCandles => _p.LookbackCandles;

    /// <summary>
    /// Điểm vào chính, được worker gọi cho từng nến vừa đóng. Đi qua 3 bước:
    /// <list type="number">
    /// <item><b>Kiểm tra đầu vào:</b> chưa đủ nến thì trả về Hold ngay.</item>
    /// <item><b>Tính EMA:</b> EMA nhanh và chậm ở nến trước và nến này.</item>
    /// <item><b>Quyết định:</b> có giao cắt không, và vị thế hiện tại có cho phép BUY/SELL không.</item>
    /// </list>
    /// </summary>
    public StrategyDecision Evaluate(StrategyContext context)
    {
        // Bước 1: nếu dữ liệu chưa đủ thì dừng ở đây với lý do (ví dụ "Warm-up: có 120/300 nến").
        if (CheckInput(context.ClosedCandles) is { } hold)
            return hold;

        // Bước 2: lấy giá đóng cửa của 300 nến cuối rồi tính 4 con số:
        // EMA nhanh/chậm ở nến trước và ở nến vừa đóng.
        IndicatorValues indicators = ComputeLast(LastCloses(context.ClosedCandles));

        // Bước 3: so sánh 4 con số đó để ra BUY, SELL hoặc Hold.
        return Decide(indicators, context);
    }

    /// <summary>
    /// Giá trị EMA fast/slow tại nến cuối của cửa sổ (dùng cả cho hiển thị),
    /// theo đúng quy tắc cửa sổ của <see cref="Evaluate"/>. Trả null nếu thiếu nến.
    /// Hàm này chỉ tính EMA để vẽ đường trên chart, không ra quyết định mua/bán.
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
        // closes[^N..] = N phần tử cuối của mảng (cú pháp "từ cuối đếm ngược" của C#).
        IndicatorValues values = ComputeLast(closes[^_p.LookbackCandles..]);
        return (values.Fast, values.Slow);
    }

    // ── Các bước nhỏ ───────────────────────────────────────────────────────────

    /// <summary>Trả về Hold nếu chưa đủ dữ liệu; ném lỗi nếu nhận nến chưa đóng.</summary>
    private StrategyDecision? CheckInput(IReadOnlyList<Candle> candles)
    {
        if (candles.Count == 0)
            return StrategyDecision.Hold("Không có nến.");

        // Nến đang chạy thì giá đóng cửa còn thay đổi; dùng nó để quyết định là "nhìn trước tương lai".
        // Đây là lỗi lập trình ở nơi gọi, nên ném lỗi thay vì lặng lẽ bỏ qua.
        for (int index = 0; index < candles.Count; index++)
            if (!candles[index].IsClosed)
                throw new ArgumentException("Chiến lược chỉ nhận nến đã đóng.", nameof(candles));

        // Chưa đủ 300 nến thì EMA chưa ổn định theo quy tắc cửa sổ cố định → chưa quyết định.
        if (candles.Count < _p.LookbackCandles)
            return StrategyDecision.Hold($"Warm-up: có {candles.Count}/{_p.LookbackCandles} nến.");

        // null = đầu vào hợp lệ, tiếp tục sang bước tính EMA.
        return null;
    }

    /// <summary>Giá đóng cửa của đúng <see cref="EmaCrossParameters.LookbackCandles"/> nến cuối.</summary>
    private decimal[] LastCloses(IReadOnlyList<Candle> candles)
    {
        decimal[] closes = new decimal[_p.LookbackCandles];
        // Nếu được đưa nhiều hơn 300 nến thì bỏ phần đầu, chỉ lấy 300 nến mới nhất.
        int offset = candles.Count - _p.LookbackCandles;
        for (int index = 0; index < closes.Length; index++)
            closes[index] = candles[offset + index].Close;
        return closes;
    }

    /// <summary>EMA fast/slow tại nến trước và nến cuối của cửa sổ.</summary>
    private IndicatorValues ComputeLast(ReadOnlySpan<decimal> closes)
    {
        // Cần cả giá trị ở "nến trước" để biết hai đường vừa cắt nhau hay đã nằm như vậy từ trước.
        (decimal fastPrev, decimal fast) = Ema.LastTwo(closes, _p.FastPeriod);
        (decimal slowPrev, decimal slow) = Ema.LastTwo(closes, _p.SlowPeriod);
        return new IndicatorValues(fastPrev, slowPrev, fast, slow);
    }

    /// <summary>Xem hai đường EMA vừa cắt lên, cắt xuống hay không cắt.</summary>
    private StrategyDecision Decide(IndicatorValues values, StrategyContext context)
    {
        // Cắt lên: nến trước EMA nhanh ≤ EMA chậm, nến này EMA nhanh > EMA chậm.
        if (Crossover.Up(values.FastPrev, values.SlowPrev, values.Fast, values.Slow))
            return OnCrossUp(values, context);
        // Cắt xuống: nến trước EMA nhanh ≥ EMA chậm, nến này EMA nhanh < EMA chậm.
        if (Crossover.Down(values.FastPrev, values.SlowPrev, values.Fast, values.Slow))
            return OnCrossDown(values, context);
        // Hai đường vẫn giữ vị trí như nến trước → không có gì mới, giữ nguyên.
        return StrategyDecision.Hold("Không có giao cắt.", values);
    }

    /// <summary>Giao cắt lên → BUY, trừ khi đã có vị thế hoặc lệnh vào đang chờ.</summary>
    /// <remarks>Không mua chồng: đã giữ hoặc đang chờ khớp lệnh mua thì bỏ qua tín hiệu mua mới.</remarks>
    private StrategyDecision OnCrossUp(IndicatorValues values, StrategyContext context) =>
        context.HasPosition || context.HasPendingEntry
            ? StrategyDecision.Hold("Giao cắt lên nhưng đã có vị thế/pending entry.", values)
            : new StrategyDecision(SignalAction.Buy, $"EMA{_p.FastPeriod} cắt lên EMA{_p.SlowPeriod}.", values);

    /// <summary>Giao cắt xuống → SELL, chỉ khi đang giữ vị thế (long-only).</summary>
    /// <remarks>Long-only nghĩa là không bán khống: chưa mua thì không có gì để bán, nên chỉ Hold.</remarks>
    private StrategyDecision OnCrossDown(IndicatorValues values, StrategyContext context) =>
        context.HasPosition
            ? new StrategyDecision(SignalAction.Sell, $"EMA{_p.FastPeriod} cắt xuống EMA{_p.SlowPeriod}.", values)
            : StrategyDecision.Hold("Giao cắt xuống nhưng không có vị thế để bán.", values);
}
