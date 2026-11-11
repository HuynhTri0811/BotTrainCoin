using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Domain.Indicators;

/// <summary>
/// RSI tính trên khối lượng mua/bán chủ động thay cho giá: đo bên mua hay bên bán đang đánh mạnh hơn.
///
/// Mua chủ động (taker buy) = lệnh mua khớp ngay vào giá bán đang chờ; bán chủ động = phần còn lại của Volume.
/// Với mỗi nến: chênh lệch = mua chủ động − bán chủ động = 2 × <see cref="Candle.TakerBuyVolume"/> − <see cref="Candle.Volume"/>.
///   - Chênh lệch dương thì cộng vào "lãi", âm thì cộng vào "lỗ", rồi tính RSI kiểu Wilder như <see cref="Rsi"/>.
///   - Flow RSI gần 100: gần như toàn nến bên mua áp đảo; gần 0: bên bán áp đảo; 50: hai bên ngang nhau.
///
/// Khác RSI giá: giá chưa chạy nhưng lực mua/bán đã dồn lên thì Flow RSI đã đổi, RSI giá thì chưa.
///
/// Tín hiệu (giống chế độ đảo chiều của <see cref="MyRsiIndicator"/>):
///   - Flow RSI cắt LÊN <see cref="Oversold"/> (30) → Long: bên mua lấy lại thế sau giai đoạn bán áp đảo.
///   - Flow RSI cắt XUỐNG <see cref="Overbought"/> (70) → Short: bên bán lấy lại thế sau giai đoạn mua áp đảo.
///
/// Nến lưu trước khi bot lấy số liệu mua chủ động không có <see cref="Candle.TakerBuyVolume"/>: cửa sổ chứa nến
/// như vậy không được tính (trả về None kèm lý do), giống cách bỏ qua cửa sổ thiếu nến.
/// </summary>
[Indicator("FlowRsi", Description = "RSI trên khối lượng mua/bán chủ động: bên mua lấy lại thế → Long, bên bán lấy lại thế → Short.")]
public sealed class FlowRsiIndicator : ICustomIndicator
{
    // ── 1. THAM SỐ ─────────────────────────────────────────────────────────────

    /// <summary>Số nến để tính RSI trên chênh lệch mua/bán.</summary>
    public int Period { get; set; } = 14;

    /// <summary>Mức bán áp đảo (mặc định 30). Flow RSI vượt lên trên mức này → có thể phát Long.</summary>
    public decimal Oversold { get; set; } = 30m;

    /// <summary>Mức mua áp đảo (mặc định 70). Flow RSI rơi xuống dưới mức này → có thể phát Short.</summary>
    public decimal Overbought { get; set; } = 70m;

    /// <summary>
    /// Số nến dùng để tính mỗi lần. RSI phụ thuộc điểm bắt đầu, nên luôn dùng đúng số nến này để chart và lệnh tự
    /// động ra cùng một giá trị.
    /// </summary>
    public int LookbackCandles { get; set; } = 200;

    // ── 2. KIỂM TRA THAM SỐ ────────────────────────────────────────────────────
    public string? Validate()
    {
        if (Period < 2) return "Period phải >= 2.";
        if (Oversold <= 0 || Overbought >= 100 || Oversold >= Overbought)
            return "Cần 0 < Oversold < Overbought < 100.";
        if (LookbackCandles < Period + 2)
            return "LookbackCandles phải lớn hơn Period ít nhất 2 nến.";
        return null;
    }

    public int RequiredCandles => LookbackCandles;

    // ── 3. CÁCH VẼ ─────────────────────────────────────────────────────────────
    public IReadOnlyList<IndicatorLine> Lines =>
        [new("flowRsi", $"Flow RSI {Period}", "#ea580c", Overlay: false, Levels: [Oversold, Overbought])];

    // ── 4. CÔNG THỨC ───────────────────────────────────────────────────────────
    // Ví dụ phát Long: nến trước Flow RSI = 24.5 (dưới 30), nến này 36.8 → "Flow RSI cắt lên 30: 24.5 → 36.8".
    public IndicatorResult Compute(IReadOnlyList<Candle> closedCandles)
    {
        if (closedCandles.Count < LookbackCandles)
            return IndicatorResult.None($"Warm-up: có {closedCandles.Count}/{LookbackCandles} nến.");

        int offset = closedCandles.Count - LookbackCandles;
        int missing = CountMissingTakerVolume(closedCandles, offset);
        if (missing > 0)
            return IndicatorResult.None(
                $"Thiếu khối lượng mua chủ động ở {missing}/{LookbackCandles} nến trong cửa sổ; chờ đủ nến mới.");

        (decimal prev, decimal now) = Rsi.LastTwo(CumulativeNetFlow(closedCandles, offset), Period);
        Dictionary<string, decimal?> values = new Dictionary<string, decimal?> { ["flowRsi"] = Math.Round(now, 2) };
        string lastCandle = DescribeLastCandle(closedCandles[^1]);
        string move = $"{prev:0.##} → {now:0.##}";

        if (Crossover.UpThrough(prev, now, Oversold))
            return new IndicatorResult(values, IndicatorSignal.Long, $"Flow RSI cắt lên {Oversold}: {move}. {lastCandle}");
        if (Crossover.DownThrough(prev, now, Overbought))
            return new IndicatorResult(values, IndicatorSignal.Short, $"Flow RSI cắt xuống {Overbought}: {move}. {lastCandle}");
        return IndicatorResult.None($"Flow RSI {now:0.##}, không cắt mức. {lastCandle}", values);
    }

    /// <summary>Số nến trong cửa sổ chưa có khối lượng mua chủ động.</summary>
    private static int CountMissingTakerVolume(IReadOnlyList<Candle> candles, int offset)
    {
        int missing = 0;
        for (int index = offset; index < candles.Count; index++)
            if (candles[index].TakerBuyVolume is null)
                missing++;
        return missing;
    }

    /// <summary>
    /// Chuỗi cộng dồn chênh lệch mua − bán, bắt đầu từ 0: phần tử i + 1 = phần tử i + chênh lệch của nến thứ i.
    /// <see cref="Rsi"/> chỉ dùng hiệu giữa hai phần tử liền nhau, nên hiệu đó chính là chênh lệch của từng nến
    /// và RSI ra đúng "RSI của mua/bán chủ động".
    /// </summary>
    private decimal[] CumulativeNetFlow(IReadOnlyList<Candle> candles, int offset)
    {
        decimal[] cumulative = new decimal[LookbackCandles + 1];
        for (int index = 0; index < LookbackCandles; index++)
        {
            Candle candle = candles[offset + index];
            decimal netFlow = 2 * candle.TakerBuyVolume!.Value - candle.Volume;
            cumulative[index + 1] = cumulative[index] + netFlow;
        }

        return cumulative;
    }

    /// <summary>Tỷ trọng mua chủ động của nến vừa đóng, để lý do trên dashboard dễ hiểu.</summary>
    private static string DescribeLastCandle(Candle candle) => candle.Volume > 0
        ? $"Nến vừa đóng: mua chủ động {candle.TakerBuyVolume!.Value / candle.Volume * 100:0.#}%."
        : "Nến vừa đóng không có giao dịch.";
}
