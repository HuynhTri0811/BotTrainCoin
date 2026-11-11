using System.Collections.Concurrent;
using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.MarketData;

/// <summary>
/// Tải nến cũ hơn nến sớm nhất đang có trong DB, để backtest có đủ lịch sử. Chỉ đi lùi về quá khứ:
/// phần mới đã có <see cref="CandleSyncService"/> lo. DB chưa có nến nào thì không tải: đồng bộ thường phải chạy
/// lần đầu trước. Nếu khoảng ghi có chồng nhau, upsert vẫn chạy lần lượt theo khóa nến nên không chèn trùng.
/// Dùng cùng kiểm tra nến (<see cref="CandleSeriesValidator"/>) và cùng upsert như đồng bộ thường.
/// </summary>
public sealed class CandleBackfillService(
    IMarketDataClient client,
    ICandleStore store,
    MarketDataOptions options,
    ILogger<CandleBackfillService> logger)
{
    /// <summary>Số request tối đa mỗi lần gọi (1000 × 1000 nến ≈ 11 năm nến 1h, ≈ 28 năm nến 15m).</summary>
    public const int MaxRequests = 1000;

    // Một lần tải tại một thời điểm cho mỗi khóa nến, để lần thứ hai trả 409 ngay thay vì tải lại cùng khoảng.
    // Việc không chèn trùng nến với các bên ghi khác do khóa trong ICandleStore.UpsertAsync lo.
    private static readonly ConcurrentDictionary<CandleKey, SemaphoreSlim> Gates = new();

    /// <summary>
    /// Tải nến từ <paramref name="fromUtc"/> đến ngay trước nến sớm nhất đã lưu.
    /// Trả null nếu đang có một lần tải khác chạy cho cùng khóa nến.
    /// </summary>
    /// <exception cref="ArgumentException">Khóa nến không thuộc sàn này, hoặc DB chưa có nến nào của khóa này.</exception>
    public async Task<CandleBackfillResult?> BackfillAsync(CandleKey key, DateTime fromUtc, CancellationToken ct)
    {
        var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, ct))
            return null;

        try
        {
            return await RunAsync(key, fromUtc, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<CandleBackfillResult> RunAsync(CandleKey key, DateTime fromUtc, CancellationToken ct)
    {
        if (key.Exchange != client.Exchange)
            throw new ArgumentException($"Chỉ tải được nến {client.Exchange}, không phải {key.Exchange}.");

        var end = (await store.GetAfterAsync(key, null, 1, closedOnly: false, ct)).FirstOrDefault()?.OpenTimeUtc
                  ?? throw new ArgumentException(
                      $"Chưa có nến {key.Symbol} {key.Interval.Code} nào; chờ đồng bộ nến chạy lần đầu rồi tải lịch sử.");
        var serverTime = await client.GetServerTimeAsync(ct);
        var start = key.Interval.OpenTimeAt(fromUtc);

        var inserted = 0;
        var rejected = 0;
        var requests = 0;
        // Đã tải tới nến sớm nhất đã lưu hoặc sàn hết nến. Cờ này, không phải start, quyết định "còn tiếp":
        // lần thoát ở request cuối không tăng start nên start < end vẫn đúng dù đã xong.
        var finished = false;
        while (start < end && requests < MaxRequests)
        {
            var klines = await client.GetKlinesAsync(key.Symbol, key.Interval, start, options.BatchSize, ct);
            requests++;
            if (klines.Count == 0)
            {
                finished = true;
                break;
            }

            // Chỉ giữ phần trước nến sớm nhất đã có: không đụng tới nến đồng bộ thường đã lưu.
            var candles = klines
                .Where(kline => kline.OpenTimeUtc < end)
                .Select(kline => kline.ToCandle(key, serverTime))
                .ToList();
            if (candles.Count > 0)
            {
                var issues = CandleSeriesValidator.Validate(candles, key.Interval);
                foreach (var issue in issues)
                    logger.LogWarning("Backfill {Key}: {Kind} tại {OpenTime:O} - {Detail}",
                        key, issue.Kind, issue.OpenTimeUtc, issue.Detail);
                var valid = CandleSeriesValidator.WithoutRejected(candles, issues);
                rejected += candles.Count - valid.Count;
                inserted += (await store.UpsertAsync(key, valid, ct)).Inserted;
            }

            if (klines.Count < options.BatchSize || candles.Count < klines.Count)
            {
                finished = true;
                break;
            }
            start = klines[^1].OpenTimeUtc + key.Interval.Duration;
        }

        var first = (await store.GetAfterAsync(key, null, 1, closedOnly: false, ct)).FirstOrDefault()?.OpenTimeUtc;
        var reachedLimit = !finished && start < end;
        logger.LogInformation("Backfill {Key} từ {From:O}: +{Inserted} nến, {Requests} request, sớm nhất {First:O}",
            key, fromUtc, inserted, requests, first);
        return new CandleBackfillResult(inserted, requests, rejected, first, reachedLimit);
    }
}
