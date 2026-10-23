using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Indicators;

/// <summary>
/// CHỈ BÁO MẪU — đọc file này để biết cách viết chỉ báo của riêng bạn.
///
/// Ý tưởng: RSI đo "đà" tăng/giảm trong <see cref="Period"/> nến, giá trị 0..100.
///   - RSI cắt LÊN qua mức quá bán (<see cref="Oversold"/>, mặc định 30)  → phát Long (giá có thể hồi lên).
///   - RSI cắt XUỐNG qua mức quá mua (<see cref="Overbought"/>, mặc định 70) → phát Short (giá có thể quay đầu).
/// Tuỳ chọn <see cref="TrendEmaPeriod"/>: chỉ nhận Long khi giá trên EMA, Short khi giá dưới EMA (lọc ngược xu hướng).
///
/// Cách dùng: khai báo trong appsettings.json → "Indicators" với "Type": "MyRsi".
/// Tên property (Period, Oversold...) chính là tên khoá trong "Parameters".
/// </summary>
[Indicator("MyRsi", Description = "RSI cắt lên mức quá bán → Long, cắt xuống mức quá mua → Short.")]
public sealed class MyRsiIndicator : ICustomIndicator
{
    // ── 1. THAM SỐ ─────────────────────────────────────────────────────────────
    // Property public có setter = tham số chỉnh được trong appsettings.json. Giá trị ở đây là mặc định.

    /// <summary>Số nến để tính RSI.</summary>
    public int Period { get; set; } = 14;

    public decimal Oversold { get; set; } = 30m;
    public decimal Overbought { get; set; } = 70m;

    /// <summary>0 = tắt bộ lọc xu hướng. Ví dụ 50: chỉ Long khi giá đóng cửa trên EMA50.</summary>
    public int TrendEmaPeriod { get; set; } = 0;

    /// <summary>
    /// Số nến dùng để tính mỗi lần. RSI/EMA phụ thuộc điểm bắt đầu, nên luôn dùng đúng số nến này
    /// để chart và lệnh tự động ra cùng một giá trị.
    /// </summary>
    public int LookbackCandles { get; set; } = 200;

    // ── 2. KIỂM TRA THAM SỐ ────────────────────────────────────────────────────
    public string? Validate()
    {
        if (Period < 2) return "Period phải >= 2.";
        if (Oversold <= 0 || Overbought >= 100 || Oversold >= Overbought)
            return "Cần 0 < Oversold < Overbought < 100.";
        if (TrendEmaPeriod < 0) return "TrendEmaPeriod phải >= 0.";
        if (LookbackCandles < Math.Max(Period, TrendEmaPeriod) + 2)
            return "LookbackCandles phải lớn hơn Period và TrendEmaPeriod ít nhất 2 nến.";
        return null;
    }

    public int RequiredCandles => LookbackCandles;

    // ── 3. CÁCH VẼ ─────────────────────────────────────────────────────────────
    // Key ("rsi", "ema") phải trùng với khoá bạn trả về trong Values ở bước 4.
    public IReadOnlyList<IndicatorLine> Lines =>
        TrendEmaPeriod > 0
            ?
            [
                new("rsi", $"RSI {Period}", "#7c3aed", Overlay: false, Levels: [Oversold, Overbought]),
                new("ema", $"EMA {TrendEmaPeriod}", "#0891b2", Overlay: true),
            ]
            : [new("rsi", $"RSI {Period}", "#7c3aed", Overlay: false, Levels: [Oversold, Overbought])];

    // ── 4. CÔNG THỨC ───────────────────────────────────────────────────────────
    // Compute chỉ ghép các bước nhỏ bên dưới; mỗi bước làm một việc nên dễ đọc, dễ sửa, dễ test.
    public IndicatorResult Compute(IReadOnlyList<Candle> closedCandles)
    {
        if (closedCandles.Count < LookbackCandles)
            return IndicatorResult.None($"Warm-up: có {closedCandles.Count}/{LookbackCandles} nến.");

        // Lấy giá đóng cửa của đúng LookbackCandles nến cuối.
        var closes = IndicatorWindow.LastCloses(closedCandles, LookbackCandles);
        var rsi = ReadRsi(closes);
        var ema = ReadTrendEma(closes);
        var close = closes[^1];
        var values = BuildValues(rsi.Now, ema);

        if (Crossover.UpThrough(rsi.Prev, rsi.Now, Oversold))
            return LongSignal(rsi, close, ema, values);
        if (Crossover.DownThrough(rsi.Prev, rsi.Now, Overbought))
            return ShortSignal(rsi, close, ema, values);
        return IndicatorResult.None($"RSI {rsi.Now:0.##}, không cắt mức.", values);
    }

    /// <summary>RSI tại nến trước (Prev) và nến vừa đóng (Now).</summary>
    private readonly record struct RsiPair(decimal Prev, decimal Now);

    // 4a. Tính RSI: chỉ cần 2 giá trị cuối nên dùng Rsi.LastTwo (không tạo mảng cho cả cửa sổ).
    private RsiPair ReadRsi(decimal[] closes)
    {
        var (prev, now) = Rsi.LastTwo(closes, Period);
        return new RsiPair(prev, now);
    }

    // 4b. EMA lọc xu hướng tại nến vừa đóng; null khi tắt bộ lọc (TrendEmaPeriod = 0).
    private decimal? ReadTrendEma(decimal[] closes) =>
        TrendEmaPeriod > 0 ? Ema.LastTwo(closes, TrendEmaPeriod).Last : null;

    // 4c. Giá trị để vẽ: khoá phải trùng với Lines ở bước 3.
    private static Dictionary<string, decimal?> BuildValues(decimal rsiNow, decimal? ema)
    {
        var values = new Dictionary<string, decimal?> { ["rsi"] = Math.Round(rsiNow, 2) };
        if (ema is not null)
            values["ema"] = ema;
        return values;
    }

    // 4d. RSI cắt lên mức quá bán → Long, trừ khi giá đang dưới EMA xu hướng.
    private IndicatorResult LongSignal(RsiPair rsi, decimal close, decimal? ema, Dictionary<string, decimal?> values) =>
        ema is { } e && close < e
            ? IndicatorResult.None($"RSI cắt lên {Oversold} nhưng giá dưới EMA{TrendEmaPeriod}.", values)
            : new IndicatorResult(values, IndicatorSignal.Long, $"RSI cắt lên {Oversold}: {rsi.Prev:0.##} → {rsi.Now:0.##}.");

    // 4e. RSI cắt xuống mức quá mua → Short, trừ khi giá đang trên EMA xu hướng.
    private IndicatorResult ShortSignal(RsiPair rsi, decimal close, decimal? ema, Dictionary<string, decimal?> values) =>
        ema is { } e && close > e
            ? IndicatorResult.None($"RSI cắt xuống {Overbought} nhưng giá trên EMA{TrendEmaPeriod}.", values)
            : new IndicatorResult(values, IndicatorSignal.Short, $"RSI cắt xuống {Overbought}: {rsi.Prev:0.##} → {rsi.Now:0.##}.");
}
