using CryptoBot.Domain.Common;

namespace CryptoBot.Domain.MarketData;

/// <summary>Một nến đáng chú ý trong báo cáo biến động.</summary>
/// <param name="OpenTimeUtc">Giờ mở của nến.</param>
/// <param name="Open">Giá mở.</param>
/// <param name="Close">Giá đóng.</param>
/// <param name="ChangePercent">% thay đổi từ giá mở đến giá đóng của nến.</param>
/// <param name="Volume">Khối lượng (coin).</param>
/// <param name="VolumeRatio">Khối lượng gấp bao nhiêu lần trung bình các nến trước; null khi chưa đủ nến để so.</param>
/// <param name="BuyPercent">Tỷ trọng mua chủ động (0–100); null khi nến chưa có khối lượng mua chủ động.</param>
public sealed record ReportCandle(
    DateTime OpenTimeUtc,
    decimal Open,
    decimal Close,
    decimal ChangePercent,
    decimal Volume,
    decimal? VolumeRatio,
    decimal? BuyPercent);

/// <summary>
/// Báo cáo biến động của một symbol trong khoảng [<see cref="FromUtc"/>, <see cref="ToUtc"/>), thường là 24 giờ,
/// tính từ nến đã đóng nằm trọn trong khoảng. Chỉ mô tả chuyện đã xảy ra, không dự đoán.
/// </summary>
/// <param name="FromUtc">Đầu khoảng (bao gồm).</param>
/// <param name="ToUtc">Cuối khoảng (không bao gồm).</param>
/// <param name="CandleCount">Số nến trong khoảng.</param>
/// <param name="Open">Giá mở của nến đầu tiên.</param>
/// <param name="Close">Giá đóng của nến cuối cùng.</param>
/// <param name="ChangePercent">% thay đổi cả khoảng.</param>
/// <param name="High">Giá cao nhất.</param>
/// <param name="HighTimeUtc">Giờ mở của nến có giá cao nhất.</param>
/// <param name="Low">Giá thấp nhất.</param>
/// <param name="LowTimeUtc">Giờ mở của nến có giá thấp nhất.</param>
/// <param name="RangePercent">(Cao nhất − thấp nhất) / giá mở, theo %.</param>
/// <param name="MaxDrawdownPercent">Cú giảm sâu nhất tính từ đỉnh trước đó (%, ≤ 0).</param>
/// <param name="MaxRallyPercent">Cú tăng mạnh nhất tính từ đáy trước đó (%, ≥ 0).</param>
/// <param name="Volume">Tổng khối lượng (coin).</param>
/// <param name="BuyPercent">Tỷ trọng mua chủ động cả khoảng; null khi có nến chưa có khối lượng mua chủ động.</param>
/// <param name="BiggestRise">Nến tăng mạnh nhất (theo % mở → đóng).</param>
/// <param name="BiggestDrop">Nến giảm mạnh nhất.</param>
/// <param name="VolumeSpikes">Các nến có khối lượng ≥ ngưỡng × trung bình các nến trước, tăng dần theo thời gian.</param>
public sealed record DailyMoveReport(
    DateTime FromUtc,
    DateTime ToUtc,
    int CandleCount,
    decimal Open,
    decimal Close,
    decimal ChangePercent,
    decimal High,
    DateTime HighTimeUtc,
    decimal Low,
    DateTime LowTimeUtc,
    decimal RangePercent,
    decimal MaxDrawdownPercent,
    decimal MaxRallyPercent,
    decimal Volume,
    decimal? BuyPercent,
    ReportCandle BiggestRise,
    ReportCandle BiggestDrop,
    IReadOnlyList<ReportCandle> VolumeSpikes)
{
    /// <summary>Số nến trước mỗi nến dùng làm trung bình khối lượng.</summary>
    public const int DefaultBaselineCandles = 20;

    /// <summary>Khối lượng gấp ngần này lần trung bình thì coi là bất thường.</summary>
    public const decimal DefaultVolumeSpikeMultiplier = 3m;

    /// <summary>
    /// Lập báo cáo từ <paramref name="candles"/> (đã đóng, tăng dần theo OpenTime, cùng một khung). Nến trước
    /// <paramref name="fromUtc"/> chỉ dùng làm trung bình khối lượng. Nến của một khung chỉ được tính khi nằm trọn
    /// trong khoảng. Trả null khi khoảng không có nến nào.
    /// </summary>
    public static DailyMoveReport? Build(
        IReadOnlyList<Candle> candles,
        DateTime fromUtc,
        DateTime toUtc,
        int baselineCandles = DefaultBaselineCandles,
        decimal volumeSpikeMultiplier = DefaultVolumeSpikeMultiplier)
    {
        if (toUtc <= fromUtc)
            throw new ArgumentException("Cuối khoảng phải sau đầu khoảng.", nameof(toUtc));
        if (baselineCandles < 1)
            throw new ArgumentOutOfRangeException(nameof(baselineCandles), "Cần ít nhất 1 nến để lấy trung bình.");
        if (volumeSpikeMultiplier <= 1)
            throw new ArgumentOutOfRangeException(nameof(volumeSpikeMultiplier), "Ngưỡng khối lượng phải lớn hơn 1.");

        List<int> window = [];
        for (int candleIndex = 0; candleIndex < candles.Count; candleIndex++)
        {
            Candle candle = candles[candleIndex];
            if (!candle.IsClosed)
                throw new ArgumentException("Báo cáo chỉ dùng nến đã đóng.", nameof(candles));
            if (candleIndex > 0 && candle.OpenTimeUtc <= candles[candleIndex - 1].OpenTimeUtc)
                throw new ArgumentException("Nến phải tăng dần theo OpenTime.", nameof(candles));
            if (candle.OpenTimeUtc >= fromUtc && candle.CloseTimeUtc <= toUtc)
                window.Add(candleIndex);
        }

        if (window.Count == 0)
            return null;

        Candle first = candles[window[0]];
        Candle last = candles[window[^1]];
        Candle high = first;
        Candle low = first;
        decimal peak = first.Open;
        decimal trough = first.Open;
        decimal maxDrawdown = 0;
        decimal maxRally = 0;
        decimal volume = 0;
        decimal? takerBuy = 0;
        List<ReportCandle> rows = new(window.Count);

        foreach (int index in window)
        {
            Candle candle = candles[index];
            if (candle.High > high.High)
                high = candle;
            if (candle.Low < low.Low)
                low = candle;

            // Không biết trong một nến đỉnh hay đáy đến trước, nên so đáy của nến với đỉnh trước nó (gồm giá mở
            // của chính nến), và ngược lại. Cách này không bao giờ phóng đại cú giảm/hồi.
            peak = Math.Max(peak, candle.Open);
            trough = Math.Min(trough, candle.Open);
            maxDrawdown = Math.Min(maxDrawdown, PercentChange.Between(peak, candle.Low));
            maxRally = Math.Max(maxRally, PercentChange.Between(trough, candle.High));
            peak = Math.Max(peak, candle.High);
            trough = Math.Min(trough, candle.Low);

            volume += candle.Volume;
            takerBuy = candle.TakerBuyVolume is { } buy ? takerBuy + buy : null;
            rows.Add(ToRow(candles, index, baselineCandles));
        }

        ReportCandle biggestRise = rows.MaxBy(candle => candle.ChangePercent)!;
        ReportCandle biggestDrop = rows.MinBy(candle => candle.ChangePercent)!;
        List<ReportCandle> spikes = rows.Where(candle => candle.VolumeRatio >= volumeSpikeMultiplier).ToList();

        return new DailyMoveReport(
            fromUtc, toUtc, window.Count,
            first.Open, last.Close, PercentChange.Between(first.Open, last.Close),
            high.High, high.OpenTimeUtc, low.Low, low.OpenTimeUtc,
            (high.High - low.Low) / first.Open * 100,
            maxDrawdown, maxRally,
            volume, takerBuy is { } totalBuy && volume > 0 ? totalBuy / volume * 100 : null,
            biggestRise, biggestDrop, spikes);
    }

    /// <summary>Nến thứ <paramref name="index"/>, so khối lượng với trung bình <paramref name="baseline"/> nến trước nó.</summary>
    private static ReportCandle ToRow(IReadOnlyList<Candle> candles, int index, int baseline)
    {
        Candle candle = candles[index];
        decimal? ratio = null;
        if (index >= baseline)
        {
            decimal average = 0;
            for (int baselineIndex = index - baseline; baselineIndex < index; baselineIndex++)
                average += candles[baselineIndex].Volume;
            average /= baseline;
            ratio = average > 0 ? candle.Volume / average : null;
        }

        decimal? buyPercent = candle.TakerBuyVolume is { } buy && candle.Volume > 0 ? buy / candle.Volume * 100 : null;
        return new ReportCandle(candle.OpenTimeUtc, candle.Open, candle.Close, PercentChange.Between(candle.Open,
            candle.Close), candle.Volume,
            ratio, buyPercent);
    }
}
