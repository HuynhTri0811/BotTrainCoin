using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.MarketData;

/// <summary>
/// Đồng bộ nến 1d lịch sử dài hạn vào bảng Candles (Exchange = sàn của client). Luôn tải lại từ nến mới nhất
/// đã lưu (bao gồm) nên nến đang chạy được cập nhật và ngày thiếu sau downtime được tải bù.
/// </summary>
public sealed class DailyHistorySyncService(
    IDailyHistoryClient client,
    ICandleStore store,
    IClock clock,
    DailyHistoryOptions options,
    ILogger<DailyHistorySyncService> logger)
{
    public async Task<int> SyncAsync(string symbol, CancellationToken ct)
    {
        var key = new CandleKey(client.Exchange, symbol, CandleInterval.D1);
        var start = await store.GetLatestOpenTimeAsync(key, ct) ??
                    options.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var inserted = 0;

        while (true)
        {
            var klines = await client.GetDailyKlinesAsync(symbol, start, options.BatchSize, ct);
            if (klines.Count == 0)
                break;

            var now = clock.UtcNow;
            var candles = klines.Select(kline => kline.ToCandle(key, now)).ToList();

            // Sàn bỏ qua ngày không có giao dịch (Gap) nên chỉ loại nến sai giá trị/thứ tự.
            var batchIssues = CandleSeriesValidator.Validate(candles, key.Interval);
            foreach (var issue in batchIssues.Where(candidate => candidate.RejectsCandle))
                logger.LogWarning("Daily history {Key}: {Kind} tại {OpenTime:O} - {Detail}",
                    key, issue.Kind, issue.OpenTimeUtc, issue.Detail);

            var valid = CandleSeriesValidator.WithoutRejected(candles, batchIssues);
            var result = await store.UpsertAsync(key, valid, ct);
            inserted += result.Inserted;
            foreach (var conflict in result.Conflicts)
                logger.LogWarning("Daily history {Key}: {Detail} ({OpenTime:O})", key, conflict.Detail, conflict.OpenTimeUtc);

            var last = candles[^1];
            if (klines.Count < options.BatchSize || !last.IsClosed)
                break;
            start = last.OpenTimeUtc + key.Interval.Duration;
        }

        if (inserted > 0)
            logger.LogInformation("Daily history {Key}: +{Inserted} nến", key, inserted);

        return inserted;
    }
}
