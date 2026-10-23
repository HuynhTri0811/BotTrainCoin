using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Indicators;

/// <summary>
/// HÀM DỰ ĐOÁN số 2 — và cũng là CHỈ BÁO MẪU: đọc file này để biết cách viết chỉ báo của riêng bạn.
///
/// RSI (Relative Strength Index) là gì: một con số từ 0 đến 100 cho biết trong <see cref="Period"/> nến gần đây
/// giá tăng nhiều hơn hay giảm nhiều hơn.
///   - RSI gần 100: gần như toàn nến tăng → "quá mua", giá đã lên nhanh, dễ chững lại.
///   - RSI gần 0  : gần như toàn nến giảm → "quá bán", giá đã xuống nhanh, dễ hồi lên.
///   - RSI 50     : tăng và giảm ngang nhau.
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

    /// <summary>Mức "quá bán" (mặc định 30). RSI vượt lên trên mức này → có thể phát Long.</summary>
    public decimal Oversold { get; set; } = 30m;

    /// <summary>Mức "quá mua" (mặc định 70). RSI rơi xuống dưới mức này → có thể phát Short.</summary>
    public decimal Overbought { get; set; } = 70m;

    /// <summary>0 = tắt bộ lọc xu hướng. Ví dụ 50: chỉ Long khi giá đóng cửa trên EMA50.</summary>
    public int TrendEmaPeriod { get; set; } = 0;

    /// <summary>
    /// Số nến dùng để tính mỗi lần. RSI/EMA phụ thuộc điểm bắt đầu, nên luôn dùng đúng số nến này
    /// để chart và lệnh tự động ra cùng một giá trị.
    /// </summary>
    public int LookbackCandles { get; set; } = 200;

    // ── 2. KIỂM TRA THAM SỐ ────────────────────────────────────────────────────
    // Gọi một lần khi ứng dụng khởi động. Trả về câu báo lỗi nếu cấu hình sai (chỉ báo sẽ bị bỏ qua),
    // trả về null nếu hợp lệ.
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

    // Bot chỉ gọi Compute khi đã có đủ số nến này (cần cho quy tắc cửa sổ cố định).
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
    //
    // Bot gọi Compute cho từng nến vừa đóng, đưa vào danh sách nến đã đóng (cũ → mới, nến cuối là nến vừa đóng).
    // Kết quả trả về gồm 3 phần:
    //   - Values : các con số để vẽ lên chart (ví dụ rsi = 47.15).
    //   - Signal : Long, Short hoặc None.
    //   - Reason : câu giải thích, hiện trên dashboard.
    //
    // Ví dụ phát Long: nến trước RSI = 28.4 (dưới 30), nến này RSI = 31.2 (trên 30) → "RSI cắt lên 30: 28.4 → 31.2".
    public IndicatorResult Compute(IReadOnlyList<Candle> closedCandles)
    {
        // Chưa đủ nến thì không tính, trả về None kèm lý do (dashboard hiện "Warm-up").
        if (closedCandles.Count < LookbackCandles)
            return IndicatorResult.None($"Warm-up: có {closedCandles.Count}/{LookbackCandles} nến.");

        // Lấy giá đóng cửa của đúng LookbackCandles nến cuối.
        decimal[] closes = IndicatorWindow.LastCloses(closedCandles, LookbackCandles);
        RsiPair rsi = ReadRsi(closes);         // 4a: RSI ở nến trước và nến vừa đóng
        decimal? ema = ReadTrendEma(closes);   // 4b: EMA xu hướng (null nếu tắt bộ lọc)
        decimal close = closes[^1];            // giá đóng cửa của nến vừa đóng (^1 = phần tử cuối)
        Dictionary<string, decimal?> values = BuildValues(rsi.Now, ema); // 4c: số liệu để vẽ

        // RSI vừa vượt LÊN mức quá bán (ví dụ 28 → 31 qua mốc 30) → xét phát Long.
        if (Crossover.UpThrough(rsi.Prev, rsi.Now, Oversold))
            return LongSignal(rsi, close, ema, values);
        // RSI vừa rơi XUỐNG dưới mức quá mua (ví dụ 72 → 68 qua mốc 70) → xét phát Short.
        if (Crossover.DownThrough(rsi.Prev, rsi.Now, Overbought))
            return ShortSignal(rsi, close, ema, values);
        // Không cắt mức nào: chỉ trả số liệu để vẽ, không có tín hiệu.
        return IndicatorResult.None($"RSI {rsi.Now:0.##}, không cắt mức.", values);
    }

    /// <summary>RSI tại nến trước (Prev) và nến vừa đóng (Now).</summary>
    private readonly record struct RsiPair(decimal Prev, decimal Now);

    // 4a. Tính RSI: chỉ cần 2 giá trị cuối nên dùng Rsi.LastTwo (không tạo mảng cho cả cửa sổ).
    //     Cần cả RSI của nến trước để biết RSI "vừa" cắt qua mức, chứ không phải đã ở bên kia từ lâu.
    private RsiPair ReadRsi(decimal[] closes)
    {
        (decimal prev, decimal now) = Rsi.LastTwo(closes, Period);
        return new RsiPair(prev, now);
    }

    // 4b. EMA lọc xu hướng tại nến vừa đóng; null khi tắt bộ lọc (TrendEmaPeriod = 0).
    //     Giá trên EMA ≈ xu hướng tăng, giá dưới EMA ≈ xu hướng giảm.
    private decimal? ReadTrendEma(decimal[] closes) =>
        TrendEmaPeriod > 0 ? Ema.LastTwo(closes, TrendEmaPeriod).Last : null;

    // 4c. Giá trị để vẽ: khoá phải trùng với Lines ở bước 3.
    //     RSI được làm tròn 2 chữ số cho dễ đọc trên chart; EMA giữ nguyên.
    private static Dictionary<string, decimal?> BuildValues(decimal rsiNow, decimal? ema)
    {
        Dictionary<string, decimal?> values = new Dictionary<string, decimal?> { ["rsi"] = Math.Round(rsiNow, 2) };
        if (ema is not null)
            values["ema"] = ema;
        return values;
    }

    // 4d. RSI cắt lên mức quá bán → Long, trừ khi giá đang dưới EMA xu hướng.
    //     Lý do lọc: giá dưới EMA nghĩa là xu hướng chung vẫn đang giảm, mua lúc này là đi ngược xu hướng.
    //     Khi tắt bộ lọc (ema = null) thì luôn phát Long.
    private IndicatorResult LongSignal(RsiPair rsi, decimal close, decimal? ema, Dictionary<string, decimal?> values) =>
        ema is { } e && close < e
            ? IndicatorResult.None($"RSI cắt lên {Oversold} nhưng giá dưới EMA{TrendEmaPeriod}.", values)
            : new IndicatorResult(values, IndicatorSignal.Long, $"RSI cắt lên {Oversold}: {rsi.Prev:0.##} → {rsi.Now:0.##}.");

    // 4e. RSI cắt xuống mức quá mua → Short, trừ khi giá đang trên EMA xu hướng.
    //     Ngược lại với 4d: giá trên EMA nghĩa là xu hướng vẫn đang tăng, bán khống lúc này là đi ngược xu hướng.
    private IndicatorResult ShortSignal(RsiPair rsi, decimal close, decimal? ema, Dictionary<string, decimal?> values) =>
        ema is { } e && close > e
            ? IndicatorResult.None($"RSI cắt xuống {Overbought} nhưng giá trên EMA{TrendEmaPeriod}.", values)
            : new IndicatorResult(values, IndicatorSignal.Short, $"RSI cắt xuống {Overbought}: {rsi.Prev:0.##} → {rsi.Now:0.##}.");
}
