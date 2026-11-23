using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.MarketData;

/// <summary>
/// Tải nến qua REST và upsert theo khóa nến. Luôn tải lại từ nến mới nhất đã lưu (bao gồm)
/// nên khoảng thiếu sau reconnect/restart được tải bù tự động. Khoảng thiếu nằm giữa các nến đã lưu trong cửa sổ gần
/// đây (sàn chưa trả hoặc nến bị loại do sai dữ liệu) được tải lại có giới hạn qua <see cref="CandleGapTracker"/>.
/// </summary>
public sealed class CandleSyncService(
    IMarketDataClient client,
    ICandleStore store,
    CandleGapTracker gaps,
    MarketDataOptions options,
    ILogger<CandleSyncService> logger)
{
    public async Task<SyncResult> SyncAsync(CandleKey key, CancellationToken ct)
    {
        var serverTime = await client.GetServerTimeAsync(ct);
        var latest = await store.GetLatestOpenTimeAsync(key, ct);
        var windowStart = key.Interval.OpenTimeAt(serverTime - key.Interval.Duration * options.BootstrapCandles);
        var start = latest ?? windowStart;
        // Nến lưu trước khi có số giao dịch/khối lượng USDT: tải lại từ nến thiếu sớm nhất trong cửa sổ
        // gần đây để bổ sung (nến đã đóng chỉ được điền thêm, giá không đổi).
        if (latest is not null &&
            await store.GetFirstMissingStatsOpenTimeAsync(key, windowStart, ct) is { } missing && missing < start)
            start = missing;

        var inserted = 0;
        var updated = 0;
        var issues = new List<CandleIssue>();
        DateTime? latestClosed = null;

        if (latest is { } latestOpen)
        {
            var repaired = await RepairGapsAsync(key, windowStart, latestOpen, serverTime, issues, ct);
            inserted += repaired.Inserted;
            updated += repaired.Updated;
        }

        for (var batch = 0; batch < options.MaxBatchesPerSync; batch++)
        {
            var klines = await client.GetKlinesAsync(key.Symbol, key.Interval, start, options.BatchSize, ct);
            if (klines.Count == 0)
                break;

            var candles = klines.Select(kline => kline.ToCandle(key, serverTime)).ToList();

            var batchIssues = CandleSeriesValidator.Validate(candles, key.Interval);
            issues.AddRange(batchIssues);

            var valid = CandleSeriesValidator.WithoutRejected(candles, batchIssues);

            var result = await store.UpsertAsync(key, valid, ct);
            inserted += result.Inserted;
            updated += result.Updated;
            issues.AddRange(result.Conflicts);

            var lastClosed = valid.LastOrDefault(candle => candle.IsClosed);
            if (lastClosed is not null)
                latestClosed = lastClosed.OpenTimeUtc;

            var last = candles[^1];
            if (klines.Count < options.BatchSize || !last.IsClosed)
                break;
            start = last.OpenTimeUtc + key.Interval.Duration;
        }

        // Lần sync chỉ nhận nến đang chạy (vừa restart) vẫn phải biết nến đóng mới nhất đã lưu.
        latestClosed ??= (await store.GetLatestAsync(key, null, 1, closedOnly: true, ct)).FirstOrDefault()?.OpenTimeUtc;

        foreach (var issue in issues)
            logger.LogWarning("Market data {Key}: {Kind} tại {OpenTime:O} - {Detail}",
                key, issue.Kind, issue.OpenTimeUtc, issue.Detail);

        if (inserted > 0)
            logger.LogInformation("Market data {Key}: +{Inserted} nến, cập nhật {Updated}", key, inserted, updated);

        return new SyncResult(key, inserted, updated, latestClosed, serverTime, issues);
    }

    /// <summary>
    /// Tải lại các khoảng thiếu giữa hai nến đã lưu trong [<paramref name="fromUtc"/>, <paramref name="toUtc"/>]
    /// (tối đa <see cref="MarketDataOptions.MaxGapRepairsPerSync"/> khoảng). Chỉ chèn nến thiếu; nến đã đóng không bị
    /// ghi đè (theo quy tắc upsert).
    /// </summary>
    private async Task<(int Inserted, int Updated)> RepairGapsAsync(CandleKey key, DateTime fromUtc, DateTime toUtc,
        DateTime serverTime, List<CandleIssue> issues, CancellationToken ct)
    {
        var step = key.Interval.Duration;
        var stored = await store.GetOpenTimesAsync(key, fromUtc, toUtc, ct);
        var inserted = 0;
        var updated = 0;
        var repairs = 0;

        for (var index = 1; index < stored.Count && repairs < options.MaxGapRepairsPerSync; index++)
        {
            var gapStart = stored[index - 1] + step;
            var gapEnd = stored[index];
            if (gapStart >= gapEnd || !gaps.TryBeginRepair(key, gapStart, serverTime, options))
                continue;

            repairs++;
            var missing = (int)Math.Min((gapEnd - gapStart) / step, options.BatchSize);
            var klines = await client.GetKlinesAsync(key.Symbol, key.Interval, gapStart, missing, ct);
            var candles = klines.Select(kline => kline.ToCandle(key, serverTime))
                .Where(candle => candle.OpenTimeUtc >= gapStart && candle.OpenTimeUtc < gapEnd).ToList();
            var batchIssues = CandleSeriesValidator.Validate(candles, key.Interval);
            issues.AddRange(batchIssues);

            var result = await store.UpsertAsync(key, CandleSeriesValidator.WithoutRejected(candles, batchIssues), ct);
            inserted += result.Inserted;
            updated += result.Updated;
            issues.AddRange(result.Conflicts);

            if (result.Inserted > 0)
                logger.LogInformation("Market data {Key}: tải bù {Inserted} nến thiếu từ {GapStart:O}",
                    key, result.Inserted, gapStart);
            if (result.Inserted == (gapEnd - gapStart) / step)
                gaps.Resolved(key, gapStart);
        }

        return (inserted, updated);
    }
}
