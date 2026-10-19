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

        var points = new List<IndicatorPoint>(visible.Count);
        for (var i = history.Count; i < series.Count; i++)
        {
            var start = Math.Max(0, i - required + 1);
            points.Add(new IndicatorPoint(series[i], Evaluate(c, series.GetRange(start, i - start + 1))));
        }

        return new IndicatorChart(c, points);
    }

    public async Task<IndicatorReading?> GetLatestAsync(string name, CancellationToken ct)
    {
        if (registry.Find(name) is not { } c)
            return null;

        var window = await candles.GetLatestAsync(c.Key, null, Math.Max(c.Indicator.RequiredCandles, 1),
            closedOnly: true, ct);
        return window.Count == 0 ? null : new IndicatorReading(c, window[^1], Evaluate(c, window));
    }

    /// <summary>
    /// Cửa sổ thiếu nến → không phát tín hiệu (giống chiến lược EMA). Lỗi trong code chỉ báo được bắt lại
    /// và trả về None kèm lý do, để một chỉ báo viết sai không làm dừng worker.
    /// </summary>
    private static IndicatorResult Evaluate(ConfiguredIndicator c, IReadOnlyList<Candle> window)
    {
        if (window.Count >= c.Indicator.RequiredCandles)
        {
            var step = c.Key.Interval.Duration;
            for (var i = window.Count - c.Indicator.RequiredCandles + 1; i < window.Count; i++)
                if (window[i].OpenTimeUtc - window[i - 1].OpenTimeUtc != step)
                    return IndicatorResult.None($"Thiếu nến trước {window[i].OpenTimeUtc:yyyy-MM-dd HH:mm} UTC.");
        }

        try
        {
            return c.Indicator.Compute(window);
        }
        catch (Exception ex)
        {
            return IndicatorResult.None($"Lỗi khi tính chỉ báo: {ex.Message}");
        }
    }
}
