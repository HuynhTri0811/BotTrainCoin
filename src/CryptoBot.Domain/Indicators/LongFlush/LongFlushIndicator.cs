using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Indicators;

/// <summary>
/// Bắt đáy sau một cú xả Long (thanh lý Long hàng loạt), nhận ra chỉ từ nến vì app chưa có dữ liệu thanh lý.
///
/// Cú xả = một nến mà:
///   - giá đâm từ giá mở xuống đáy nến ít nhất <see cref="DropAtr"/> lần ATR (ATR của các nến trước đó), và
///   - khối lượng ít nhất <see cref="VolumeMultiplier"/> lần khối lượng trung bình của <see cref="VolumePeriod"/>
///     nến trước.
/// Trên BTC 1h (06/2023 → 10/2024), khoảng 60% nến như vậy trùng với giờ thanh lý Long lớn nhất (top 5%).
///
/// Không mua ngay ở cú xả: giá thường còn rơi tiếp. Chỉ báo chờ tối đa <see cref="ConfirmCandles"/> nến để có nến
/// xác nhận đầu tiên — nến xanh (đóng cao hơn mở) và đóng trên mức hồi = giữa giá mở và đáy của nến xả → Long.
/// Có nến nào thủng đáy nến xả trước khi xác nhận thì bỏ cú xả đó.
///
/// Chỉ phát Long, không bao giờ phát Short: khi backtest Futures chọn "chỉ Long" và đặt chốt lời/cắt lỗ, vì không có
/// tín hiệu nào đóng lệnh.
///
/// Kết quả kiểm tra (BTC 1h và ETH 1h, 2020–11/2026, phí + trượt 0,2%/vòng): mua ngay ở cú xả còn lỗ hơn mua bất kỳ
/// lúc nào vì giá hay rơi tiếp; chờ xác nhận như trên thì đỡ hơn nhưng vẫn chưa có lãi. Backtest Futures chỉ Long,
/// 50 USDT × 2x: TP 6% / SL 3% gần hòa vốn (hệ số lãi 0,97 trên cả hai đồng), chốt lời/cắt lỗ chặt hơn thì lỗ.
/// Chưa phải lợi thế.
///
/// Cách dùng: khai báo trong appsettings.json → "Indicators" với "Type": "LongFlush".
/// </summary>
[Indicator("LongFlush",
    Description = "Cú xả Long (đâm mạnh + khối lượng lớn) rồi nến hồi xác nhận → Long. Chỉ phát Long.")]
public sealed class LongFlushIndicator : ICustomIndicator
{
    // ── 1. THAM SỐ ─────────────────────────────────────────────────────────────

    /// <summary>Số nến tính ATR (độ biến động trung bình mỗi nến, kiểu Wilder).</summary>
    public int AtrPeriod { get; set; } = 14;

    /// <summary>Nến xả phải đâm từ giá mở xuống đáy ít nhất bấy nhiêu lần ATR.</summary>
    public decimal DropAtr { get; set; } = 2m;

    /// <summary>Số nến trước đó để tính khối lượng trung bình.</summary>
    public int VolumePeriod { get; set; } = 20;

    /// <summary>Nến xả phải có khối lượng ít nhất bấy nhiêu lần khối lượng trung bình.</summary>
    public decimal VolumeMultiplier { get; set; } = 2m;

    /// <summary>Chờ nến xác nhận tối đa bấy nhiêu nến sau nến xả.</summary>
    public int ConfirmCandles { get; set; } = 6;

    /// <summary>
    /// Số nến dùng để tính mỗi lần. ATR phụ thuộc điểm bắt đầu, nên luôn dùng đúng số nến này để chart, lệnh tự động
    /// và backtest ra cùng một giá trị.
    /// </summary>
    public int LookbackCandles { get; set; } = 100;

    // ── 2. KIỂM TRA THAM SỐ ────────────────────────────────────────────────────
    public string? Validate()
    {
        if (AtrPeriod < 1) return "AtrPeriod phải >= 1.";
        if (DropAtr <= 0) return "DropAtr phải > 0.";
        if (VolumePeriod < 1) return "VolumePeriod phải >= 1.";
        if (VolumeMultiplier <= 0) return "VolumeMultiplier phải > 0.";
        if (ConfirmCandles < 1) return "ConfirmCandles phải >= 1.";
        if (LookbackCandles < MinimumLookback) return $"LookbackCandles phải >= {MinimumLookback}.";
        return null;
    }

    // Nến xả sớm nhất được xét nằm trước nến cuối ConfirmCandles nến, và cần đủ nến phía trước để có ATR
    // (AtrPeriod + 1 nến) và khối lượng trung bình (VolumePeriod nến).
    private int MinimumLookback => Math.Max(AtrPeriod + 1, VolumePeriod) + ConfirmCandles + 1;

    public int RequiredCandles => LookbackCandles;

    // ── 3. CÁCH VẼ ─────────────────────────────────────────────────────────────
    public IReadOnlyList<IndicatorLine> Lines =>
    [
        new("drop", "Đâm xuống (× ATR)", "#dc2626", Overlay: false, Levels: [DropAtr]),
        new("volume", "Khối lượng (× TB)", "#2563eb", Overlay: false, Levels: [VolumeMultiplier]),
        new("reclaim", "Mức hồi của cú xả", "#16a34a", Overlay: true),
    ];

    // ── 4. CÔNG THỨC ───────────────────────────────────────────────────────────
    public IndicatorResult Compute(IReadOnlyList<Candle> closedCandles)
    {
        if (closedCandles.Count < LookbackCandles)
            return IndicatorResult.None($"Warm-up: có {closedCandles.Count}/{LookbackCandles} nến.");

        Window window = new(closedCandles, closedCandles.Count - LookbackCandles, LookbackCandles);
        decimal?[] atrBefore = AtrBefore(window);
        int last = window.Count - 1;

        decimal? drop = DropOf(window, atrBefore, last);
        decimal? volumeRatio = VolumeRatio(window, last);
        int? flush = LatestLiveFlush(window, atrBefore, last);
        decimal? reclaim = flush is { } flushIndex ? ReclaimLevel(window[flushIndex]) : null;
        Dictionary<string, decimal?> values = new Dictionary<string, decimal?>
        {
            ["drop"] = drop is { } dropValue ? Math.Round(dropValue, 2) : null,
            ["volume"] = volumeRatio is { } ratio ? Math.Round(ratio, 2) : null,
            ["reclaim"] = reclaim,
        };

        for (int index = last - 1; index >= last - ConfirmCandles; index--)
        {
            if (IsFlush(window, atrBefore, index) && ConfirmsFirstAt(window, index, last))
            {
                Candle flushCandle = window[index];
                return new IndicatorResult(values, IndicatorSignal.Long,
                    $"Xả Long lúc {flushCandle.OpenTimeUtc:yyyy-MM-dd HH:mm} UTC " +
                    $"(đâm {DropOf(window, atrBefore, index):0.#} ATR, " +
                    $"khối lượng {VolumeRatio(window, index):0.#}× TB), " +
                    $"nến hồi đóng trên {ReclaimLevel(flushCandle):0.########}.");
            }
        }

        string reason = reclaim is { } level
            ? $"Có cú xả, chờ nến xanh đóng trên {level:0.########}."
            : $"Chưa có cú xả: đâm {drop:0.##} ATR, khối lượng {volumeRatio:0.##}× TB.";
        return IndicatorResult.None(reason, values);
    }

    /// <summary><paramref name="Count"/> nến cuối của danh sách, đánh số lại từ 0.</summary>
    private readonly record struct Window(IReadOnlyList<Candle> Candles, int Offset, int Count)
    {
        public Candle this[int index] => Candles[Offset + index];
    }

    // ATR tính đến hết nến TRƯỚC vị trí index (null khi chưa đủ AtrPeriod biên độ). So nến xả với ATR của các nến trước
    // nó, để chính cú xả không làm ATR to lên rồi tự làm mình trông nhỏ đi.
    private decimal?[] AtrBefore(Window window)
    {
        decimal?[] result = new decimal?[window.Count];
        decimal sum = 0m;
        decimal atr = 0m;
        for (int index = 1; index < window.Count - 1; index++)
        {
            decimal trueRange = TrueRange(window[index], window[index - 1]);
            if (index < AtrPeriod)
            {
                sum += trueRange;
                continue;
            }

            atr = index == AtrPeriod ? (sum + trueRange) / AtrPeriod : (atr * (AtrPeriod - 1) + trueRange) / AtrPeriod;
            result[index + 1] = atr;
        }

        return result;
    }

    private static decimal TrueRange(Candle candle, Candle previous) =>
        Math.Max(candle.High - candle.Low,
            Math.Max(Math.Abs(candle.High - previous.Close), Math.Abs(candle.Low - previous.Close)));

    // Đâm xuống bao nhiêu lần ATR: (giá mở − đáy) / ATR các nến trước.
    private static decimal? DropOf(Window window, decimal?[] atrBefore, int index) =>
        atrBefore[index] is { } atr && atr > 0 ? (window[index].Open - window[index].Low) / atr : null;

    // Khối lượng nến index so với trung bình VolumePeriod nến trước nó.
    private decimal? VolumeRatio(Window window, int index)
    {
        if (index < VolumePeriod)
            return null;
        decimal sum = 0m;
        for (int previous = index - VolumePeriod; previous < index; previous++)
            sum += window[previous].Volume;
        decimal average = sum / VolumePeriod;
        return average > 0 ? window[index].Volume / average : null;
    }

    private bool IsFlush(Window window, decimal?[] atrBefore, int index) =>
        DropOf(window, atrBefore, index) is { } drop && drop >= DropAtr
        && VolumeRatio(window, index) is { } ratio && ratio >= VolumeMultiplier;

    // Mức hồi = giữa giá mở và đáy của nến xả.
    private static decimal ReclaimLevel(Candle flush) => (flush.Open + flush.Low) / 2;

    private static bool Confirms(Candle candle, Candle flush) =>
        candle.Close > candle.Open && candle.Close > ReclaimLevel(flush);

    // Nến last là nến xác nhận ĐẦU TIÊN của cú xả ở vị trí flush, và chưa nến nào từ sau cú xả thủng đáy của nó.
    private static bool ConfirmsFirstAt(Window window, int flush, int last)
    {
        Candle flushCandle = window[flush];
        for (int index = flush + 1; index <= last; index++)
        {
            if (window[index].Low < flushCandle.Low)
                return false;
            if (Confirms(window[index], flushCandle))
                return index == last;
        }

        return false;
    }

    // Cú xả gần nhất (tính cả nến cuối) còn đang chờ xác nhận: chưa bị thủng đáy, chưa có nến xác nhận nào trước
    // nến cuối.
    private int? LatestLiveFlush(Window window, decimal?[] atrBefore, int last)
    {
        for (int flush = last; flush >= Math.Max(1, last - ConfirmCandles); flush--)
        {
            if (!IsFlush(window, atrBefore, flush))
                continue;
            Candle flushCandle = window[flush];
            bool live = true;
            for (int index = flush + 1; index <= last && live; index++)
                live = window[index].Low >= flushCandle.Low && (index == last || !Confirms(window[index], flushCandle));
            if (live)
                return flush;
        }

        return null;
    }
}
