using CryptoBot.Domain.Common;
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

        IReadOnlyList<Candle> window =
            new ListSlice<Candle>(closedCandles, closedCandles.Count - LookbackCandles, LookbackCandles);
        int last = window.Count - 1;
        int first = last - ConfirmCandles; // cú xả sớm nhất còn được chờ xác nhận ở nến cuối
        Measure[] tail = MeasureTail(window, first);

        int? signalFlush = SignalFlush(window, tail, first, last);
        // Có tín hiệu thì vẽ mức hồi của chính cú xả phát tín hiệu, không thì của cú xả mới nhất còn đang chờ.
        int? shownFlush = signalFlush ?? LatestLiveFlush(window, tail, first, last);
        decimal? reclaim = shownFlush is { } shown ? ReclaimLevel(window[shown]) : null;
        Measure now = tail[^1];
        Dictionary<string, decimal?> values = new Dictionary<string, decimal?>
        {
            ["drop"] = now.Drop is { } drop ? Math.Round(drop, 2) : null,
            ["volume"] = now.VolumeRatio is { } ratio ? Math.Round(ratio, 2) : null,
            ["reclaim"] = reclaim,
        };

        if (signalFlush is { } flush)
        {
            Candle flushCandle = window[flush];
            Measure measure = tail[flush - first];
            return new IndicatorResult(values, IndicatorSignal.Long,
                $"Xả Long lúc {flushCandle.OpenTimeUtc:yyyy-MM-dd HH:mm} UTC " +
                $"(đâm {measure.Drop:0.#} ATR, khối lượng {measure.VolumeRatio:0.#}× TB), " +
                $"nến hồi đóng trên {ReclaimLevel(flushCandle):0.########}.");
        }

        string reason = reclaim is { } level
            ? $"Có cú xả, chờ nến xanh đóng trên {level:0.########}."
            : $"Chưa có cú xả: đâm {now.Drop:0.##} ATR, khối lượng {now.VolumeRatio:0.##}× TB.";
        return IndicatorResult.None(reason, values);
    }

    /// <summary>
    /// Số đo của một nến: đâm từ giá mở xuống đáy bao nhiêu lần ATR các nến trước, và khối lượng gấp bao nhiêu lần
    /// trung bình <see cref="VolumePeriod"/> nến trước. null khi ATR hoặc khối lượng trung bình bằng 0.
    /// </summary>
    private readonly record struct Measure(decimal? Drop, decimal? VolumeRatio);

    // Một lượt qua cửa sổ: ATR kiểu Wilder và tổng khối lượng trượt, chỉ giữ số đo của các nến từ vị trí first đến
    // cuối. ATR ở mỗi nến tính đến hết nến TRƯỚC nó, để chính cú xả không làm ATR to lên rồi tự làm mình trông nhỏ đi.
    private Measure[] MeasureTail(IReadOnlyList<Candle> window, int first)
    {
        Measure[] tail = new Measure[window.Count - first];
        decimal rangeSum = 0m;
        decimal atr = 0m;
        bool hasAtr = false;
        decimal volumeSum = 0m; // khối lượng của VolumePeriod nến ngay trước nến đang xét
        for (int index = 0; index < window.Count; index++)
        {
            Candle candle = window[index];
            if (index >= first)
            {
                decimal? drop = hasAtr && atr > 0 ? (candle.Open - candle.Low) / atr : null;
                decimal average = volumeSum / VolumePeriod;
                decimal? ratio = index >= VolumePeriod && average > 0 ? candle.Volume / average : null;
                tail[index - first] = new Measure(drop, ratio);
            }

            if (index >= 1)
            {
                decimal trueRange = TrueRange(candle, window[index - 1]);
                if (index < AtrPeriod)
                {
                    rangeSum += trueRange;
                }
                else
                {
                    atr = index == AtrPeriod
                        ? (rangeSum + trueRange) / AtrPeriod
                        : (atr * (AtrPeriod - 1) + trueRange) / AtrPeriod;
                    hasAtr = true;
                }
            }

            volumeSum += candle.Volume;
            if (index >= VolumePeriod)
                volumeSum -= window[index - VolumePeriod].Volume;
        }

        return tail;
    }

    private static decimal TrueRange(Candle candle, Candle previous) =>
        Math.Max(candle.High - candle.Low,
            Math.Max(Math.Abs(candle.High - previous.Close), Math.Abs(candle.Low - previous.Close)));

    private bool IsFlush(Measure measure) =>
        measure.Drop is { } drop && drop >= DropAtr && measure.VolumeRatio is { } ratio && ratio >= VolumeMultiplier;

    // Mức hồi = giữa giá mở và đáy của nến xả.
    private static decimal ReclaimLevel(Candle flush) => (flush.Open + flush.Low) / 2;

    private static bool Confirms(Candle candle, Candle flush) =>
        candle.Close > candle.Open && candle.Close > ReclaimLevel(flush);

    // Cú xả mới nhất (trước nến cuối, trong khoảng chờ) mà nến cuối là nến xác nhận đầu tiên của nó; null nếu không có.
    private int? SignalFlush(IReadOnlyList<Candle> window, Measure[] tail, int first, int last)
    {
        for (int flush = last - 1; flush >= first; flush--)
        {
            if (IsFlush(tail[flush - first]) && ConfirmsFirstAt(window, flush, last))
                return flush;
        }

        return null;
    }

    // Nến last là nến xác nhận ĐẦU TIÊN của cú xả ở vị trí flush, và chưa nến nào từ sau cú xả thủng đáy của nó.
    private static bool ConfirmsFirstAt(IReadOnlyList<Candle> window, int flush, int last)
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

    // Cú xả mới nhất (tính cả nến cuối) còn đang chờ xác nhận: chưa bị thủng đáy, chưa có nến xác nhận nào trước
    // nến cuối.
    private int? LatestLiveFlush(IReadOnlyList<Candle> window, Measure[] tail, int first, int last)
    {
        for (int flush = last; flush >= first; flush--)
        {
            if (!IsFlush(tail[flush - first]))
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
