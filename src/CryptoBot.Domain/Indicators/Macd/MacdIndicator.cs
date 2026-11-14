using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Indicators;

/// <summary>
/// MACD bám xu hướng, mặc định dùng chu kỳ chậm (96/208/72 = MACD chuẩn 12/26/9 kéo dài 8 lần).
///
/// MACD (Moving Average Convergence Divergence) là gì:
///   - Đường MACD = EMA nhanh − EMA chậm của giá đóng cửa. Dương: giá gần đây cao hơn mức trung bình dài → đà tăng.
///   - Đường tín hiệu = EMA của chính đường MACD, đi chậm hơn MACD một nhịp.
///
/// Tín hiệu:
///   - MACD cắt LÊN đường tín hiệu → Long (đà tăng đang mạnh lên).
///   - MACD cắt XUỐNG đường tín hiệu → Short (đà giảm đang mạnh lên).
///
/// Khác <see cref="MyRsiIndicator"/> kiểu đảo chiều: chỉ báo này đi THEO xu hướng chứ không bắt đáy/đỉnh. Tỉ lệ thắng
/// thấp (khoảng 30%), lãi đến từ số ít lệnh đi xa, nên hợp với chốt lời xa hơn cắt lỗ (ví dụ TP 10% / SL 3%).
/// Chu kỳ chuẩn 12/26/9 trên nến 1h cho quá nhiều tín hiệu nhiễu; vì vậy mặc định ở đây là bản chậm.
///
/// Cách dùng: khai báo trong appsettings.json → "Indicators" với "Type": "Macd".
/// </summary>
[Indicator("Macd", Description = "MACD chậm bám xu hướng: MACD cắt lên đường tín hiệu → Long, cắt xuống → Short.")]
public sealed class MacdIndicator : ICustomIndicator
{
    // ── 1. THAM SỐ ─────────────────────────────────────────────────────────────

    /// <summary>Số nến của EMA nhanh.</summary>
    public int FastPeriod { get; set; } = 96;

    /// <summary>Số nến của EMA chậm. Phải lớn hơn <see cref="FastPeriod"/>.</summary>
    public int SlowPeriod { get; set; } = 208;

    /// <summary>
    /// Số nến của đường tín hiệu (EMA của đường MACD). Phải &gt;= 2: EMA 1 chính là đường MACD nên hai đường không bao
    /// giờ cắt nhau.
    /// </summary>
    public int SignalPeriod { get; set; } = 72;

    /// <summary>
    /// Số nến dùng để tính mỗi lần. EMA phụ thuộc điểm bắt đầu, nên luôn dùng đúng số nến này để chart, lệnh tự động
    /// và backtest ra cùng một giá trị.
    /// </summary>
    /// <remarks>
    /// Cửa sổ có nến thiếu thì bot không tính (như mọi chỉ báo), nên một nến thiếu làm chỉ báo im lặng đúng bằng số nến
    /// này. 600 (khoảng 3 lần <see cref="SlowPeriod"/>) đủ để EMA ổn định mà chỉ im lặng khoảng 25 ngày trên nến 1h;
    /// backtest BTC 1h 2020–2026 cho kết quả gần như giống cửa sổ 1000. Backtest nhận tối đa 1000 nến.
    /// </remarks>
    public int LookbackCandles { get; set; } = 600;

    // ── 2. KIỂM TRA THAM SỐ ────────────────────────────────────────────────────
    public string? Validate()
    {
        if (FastPeriod < 1) return "FastPeriod phải >= 1.";
        if (SlowPeriod <= FastPeriod) return "SlowPeriod phải lớn hơn FastPeriod.";
        // EMA 1 của đường MACD chính là đường MACD: hai đường trùng nhau, không bao giờ cắt nên không có tín hiệu.
        if (SignalPeriod < 2) return "SignalPeriod phải >= 2.";
        // Đường MACD có từ nến thứ SlowPeriod; đường tín hiệu cần thêm SignalPeriod + 1 giá trị MACD để có 2 điểm cuối.
        // Cộng bằng long: hai chu kỳ rất lớn cộng bằng int sẽ tràn thành số âm và lọt qua kiểm tra.
        if (LookbackCandles < (long)SlowPeriod + SignalPeriod)
            return "LookbackCandles phải >= SlowPeriod + SignalPeriod.";
        return null;
    }

    public int RequiredCandles => LookbackCandles;

    // ── 3. CÁCH VẼ ─────────────────────────────────────────────────────────────
    public IReadOnlyList<IndicatorLine> Lines =>
    [
        new("macd", $"MACD {FastPeriod}/{SlowPeriod}", "#2563eb", Overlay: false, Levels: [0m]),
        new("signal", $"Tín hiệu {SignalPeriod}", "#ea580c", Overlay: false),
    ];

    // ── 4. CÔNG THỨC ───────────────────────────────────────────────────────────
    public IndicatorResult Compute(IReadOnlyList<Candle> closedCandles)
    {
        if (closedCandles.Count < LookbackCandles)
            return IndicatorResult.None($"Warm-up: có {closedCandles.Count}/{LookbackCandles} nến.");

        decimal[] closes = IndicatorWindow.LastCloses(closedCandles, LookbackCandles);
        decimal[] macd = MacdLine(closes);
        // Cần giá trị ở nến trước để biết hai đường "vừa" cắt nhau, chứ không phải đã nằm khác phía từ lâu.
        (decimal signalPrev, decimal signalNow) = Ema.LastTwo(macd, SignalPeriod);
        decimal macdPrev = macd[^2];
        decimal macdNow = macd[^1];

        Dictionary<string, decimal?> values = new Dictionary<string, decimal?>
        {
            ["macd"] = Math.Round(macdNow, 4),
            ["signal"] = Math.Round(signalNow, 4),
        };
        string move = $"MACD {macdNow:0.##}, tín hiệu {signalNow:0.##}";

        if (Crossover.Up(macdPrev, signalPrev, macdNow, signalNow))
            return new IndicatorResult(values, IndicatorSignal.Long, $"MACD cắt lên đường tín hiệu: {move}.");
        if (Crossover.Down(macdPrev, signalPrev, macdNow, signalNow))
            return new IndicatorResult(values, IndicatorSignal.Short, $"MACD cắt xuống đường tín hiệu: {move}.");
        return IndicatorResult.None($"{move}, không cắt nhau.", values);
    }

    // Đường MACD = EMA nhanh − EMA chậm, từ nến đầu tiên có EMA chậm (vị trí SlowPeriod − 1) đến nến cuối.
    // Một vòng lặp cập nhật cả hai EMA, cùng công thức với Ema.Calculate (seed bằng trung bình cộng, rồi
    // EMA = alpha × giá + (1 − alpha) × EMA cũ), nên ra đúng giá trị như Ema.Calculate mà không tạo mảng trung gian.
    private decimal[] MacdLine(decimal[] closes)
    {
        decimal fastAlpha = 2m / (FastPeriod + 1);
        decimal slowAlpha = 2m / (SlowPeriod + 1);
        decimal fastEma = Average(closes, FastPeriod);
        for (int index = FastPeriod; index < SlowPeriod; index++)
            fastEma = fastAlpha * closes[index] + (1 - fastAlpha) * fastEma;
        decimal slowEma = Average(closes, SlowPeriod);

        int first = SlowPeriod - 1;
        decimal[] line = new decimal[closes.Length - first];
        line[0] = fastEma - slowEma;
        for (int index = SlowPeriod; index < closes.Length; index++)
        {
            fastEma = fastAlpha * closes[index] + (1 - fastAlpha) * fastEma;
            slowEma = slowAlpha * closes[index] + (1 - slowAlpha) * slowEma;
            line[index - first] = fastEma - slowEma;
        }
        return line;
    }

    // Trung bình cộng của count giá đầu tiên: điểm bắt đầu của EMA.
    private static decimal Average(decimal[] closes, int count)
    {
        decimal sum = 0m;
        for (int index = 0; index < count; index++)
            sum += closes[index];
        return sum / count;
    }
}
