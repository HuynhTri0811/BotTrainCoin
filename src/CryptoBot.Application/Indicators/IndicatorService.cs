using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Indicators;

public sealed record IndicatorPoint(Candle Candle, IndicatorResult Result);

public sealed record IndicatorChart(ConfiguredIndicator Indicator, IReadOnlyList<IndicatorPoint> Points);

/// <summary>Giá trị chỉ báo tại nến đã đóng mới nhất.</summary>
public sealed record IndicatorReading(ConfiguredIndicator Indicator, Candle Candle, IndicatorResult Result);

/// <summary>
/// Tính chỉ báo trên nến trong DB. Chart và lệnh tự động dùng cùng quy tắc cửa sổ
/// (<see cref="ICustomIndicator.RequiredCandles"/> nến đã đóng kết thúc tại nến đánh giá).
/// </summary>
public sealed class IndicatorService(IndicatorRegistry registry, ICandleStore candles)
{
    public const int MaxLimit = 1000;

    public async Task<IndicatorChart?> GetChartAsync(string name, int limit, CancellationToken ct)
    {
        if (registry.Find(name) is not { } c)
            return null;

        limit = Math.Clamp(limit, 1, MaxLimit);
        var visible = await candles.GetLatestAsync(c.Key, null, limit, closedOnly: true, ct);
        if (visible.Count == 0)
            return new IndicatorChart(c, []);

        var required = Math.Max(c.Indicator.RequiredCandles, 1);
        var history = await candles.GetLatestAsync(c.Key, visible[0].OpenTimeUtc, required - 1, closedOnly: true, ct);
        var series = history.Concat(visible).ToList();
        var gaps = GapIndex.Build(series, c.Key.Interval.Duration);

        var points = new List<IndicatorPoint>(visible.Count);
        for (var i = history.Count; i < series.Count; i++)
        {
            var start = Math.Max(0, i - required + 1);
            var window = new ListSlice<Candle>(series, start, i - start + 1);
            points.Add(new IndicatorPoint(series[i], Evaluate(c, window, gaps.FirstIn(series, start, i, c.Indicator.RequiredCandles))));
        }

        return new IndicatorChart(c, points);
    }

    public async Task<IndicatorReading?> GetLatestAsync(string name, CancellationToken ct)
    {
        if (registry.Find(name) is not { } c)
            return null;

        var window = await candles.GetLatestAsync(c.Key, null, Math.Max(c.Indicator.RequiredCandles, 1),
            closedOnly: true, ct);
        if (window.Count == 0)
            return null;

        var gap = GapIndex.Build(window, c.Key.Interval.Duration)
            .FirstIn(window, 0, window.Count - 1, c.Indicator.RequiredCandles);
        return new IndicatorReading(c, window[^1], Evaluate(c, window, gap));
    }

    /// <summary>
    /// Cửa sổ thiếu nến → không phát tín hiệu (giống chiến lược EMA). Lỗi trong code chỉ báo được bắt lại
    /// và trả về None kèm lý do, để một chỉ báo viết sai không làm dừng worker.
    /// </summary>
    private static IndicatorResult Evaluate(ConfiguredIndicator c, IReadOnlyList<Candle> window, Candle? gapAt) =>
        gapAt is not null
            ? IndicatorResult.None($"Thiếu nến trước {gapAt.OpenTimeUtc:yyyy-MM-dd HH:mm} UTC.")
            : SafeCompute(c, window);

    private static IndicatorResult SafeCompute(ConfiguredIndicator c, IReadOnlyList<Candle> window)
    {
        try
        {
            return c.Indicator.Compute(window);
        }
        catch (Exception ex)
        {
            return IndicatorResult.None($"Lỗi khi tính chỉ báo: {ex.Message}");
        }
    }

    /// <summary>
    /// Vị trí các nến đứng sau một khoảng thiếu, tính một lần cho cả chuỗi. Nhờ vậy mỗi cửa sổ chỉ cần
    /// tra một mảng thay vì quét lại toàn bộ 200–300 nến.
    /// </summary>
    private sealed class GapIndex
    {
        /// <summary>next[j] = vị trí nhỏ nhất g ≥ j mà nến g cách nến g−1 khác một khung; không có thì int.MaxValue.</summary>
        private readonly int[] _next;

        private GapIndex(int[] next) => _next = next;

        public static GapIndex Build(IReadOnlyList<Candle> series, TimeSpan step)
        {
            var next = new int[series.Count + 1];
            next[series.Count] = int.MaxValue;
            for (var j = series.Count - 1; j >= 0; j--)
                next[j] = j > 0 && series[j].OpenTimeUtc - series[j - 1].OpenTimeUtc != step ? j : next[j + 1];
            return new GapIndex(next);
        }

        /// <summary>
        /// Nến đầu tiên sau khoảng thiếu trong <paramref name="required"/> nến cuối của cửa sổ [start..end]
        /// (cùng phạm vi kiểm tra như trước), hoặc null nếu cửa sổ chưa đủ nến hay không thiếu nến nào.
        /// </summary>
        public Candle? FirstIn(IReadOnlyList<Candle> series, int start, int end, int required)
        {
            if (end - start + 1 < required)
                return null;
            var from = Math.Max(end - required + 2, start + 1);
            if (from > end)
                return null;
            var g = _next[from];
            return g <= end ? series[g] : null;
        }
    }
}
