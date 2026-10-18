using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.MarketData;

public sealed class MarketDataOptions
{
    /// <summary>Số nến tải lần đầu khi DB chưa có dữ liệu.</summary>
    public int BootstrapCandles { get; set; } = 1000;

    /// <summary>Số nến mỗi request REST (Binance tối đa 1000).</summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>Số batch tối đa mỗi lần sync, tránh một lần sync kéo dài vô hạn.</summary>
    public int MaxBatchesPerSync { get; set; } = 20;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Không sync thành công trong khoảng này thì dữ liệu bị coi là stale.</summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(2);
}

public sealed record SyncResult(
    CandleKey Key,
    int Inserted,
    int Updated,
    DateTime? LatestClosedOpenTimeUtc,
    DateTime ServerTimeUtc,
    IReadOnlyList<CandleIssue> Issues);

/// <summary>
/// Tải nến qua REST và upsert theo khóa nến. Luôn tải lại từ nến mới nhất đã lưu (bao gồm)
/// nên khoảng thiếu sau reconnect/restart được tải bù tự động.
/// </summary>
public sealed class CandleSyncService(
    IMarketDataClient client,
    ICandleStore store,
    MarketDataOptions options,
    ILogger<CandleSyncService> logger)
{
    public async Task<SyncResult> SyncAsync(CandleKey key, CancellationToken ct)
    {
        var serverTime = await client.GetServerTimeAsync(ct);
        var latest = await store.GetLatestOpenTimeAsync(key, ct);
        var start = latest ?? AlignDown(serverTime - key.Interval.Duration * options.BootstrapCandles, key.Interval);

        var inserted = 0;
        var updated = 0;
        var issues = new List<CandleIssue>();
        DateTime? latestClosed = null;

        for (var batch = 0; batch < options.MaxBatchesPerSync; batch++)
        {
            var klines = await client.GetKlinesAsync(key.Symbol, key.Interval, start, options.BatchSize, ct);
            if (klines.Count == 0)
                break;

            var candles = klines
                .Select(k => new Candle(key.Exchange, key.Symbol, key.Interval, k.OpenTimeUtc,
                    k.Open, k.High, k.Low, k.Close, k.Volume,
                    isClosed: serverTime >= k.OpenTimeUtc + key.Interval.Duration,
                    receivedAtUtc: serverTime))
                .ToList();

            var batchIssues = CandleSeriesValidator.Validate(candles, key.Interval);
            issues.AddRange(batchIssues);

            // Không sửa dữ liệu âm thầm: nến có giá trị không hợp lệ không được lưu.
            var invalid = batchIssues
                .Where(i => i.Kind is CandleIssueKind.InvalidValues or CandleIssueKind.Duplicate or CandleIssueKind.OutOfOrder)
                .Select(i => i.OpenTimeUtc)
                .ToHashSet();
            var valid = candles.Where(c => !invalid.Contains(c.OpenTimeUtc)).ToList();

            var result = await store.UpsertAsync(key, valid, ct);
            inserted += result.Inserted;
            updated += result.Updated;
            issues.AddRange(result.Conflicts);

            var lastClosed = valid.LastOrDefault(c => c.IsClosed);
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

    private static DateTime AlignDown(DateTime t, CandleInterval interval) =>
        new(t.Ticks - t.Ticks % interval.Duration.Ticks, DateTimeKind.Utc);
}
