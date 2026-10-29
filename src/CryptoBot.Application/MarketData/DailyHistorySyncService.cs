using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.MarketData;

public sealed class DailyHistoryOptions
{
    /// <summary>Tải nến từ ngày này khi DB chưa có dữ liệu của symbol.</summary>
    public DateOnly StartDate { get; set; } = new(2016, 1, 1);

    /// <summary>Nến ngày chỉ đổi một lần mỗi ngày nên không cần poll dày.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Số nến mỗi request REST (Bitfinex tối đa 10000).</summary>
    public int BatchSize { get; set; } = 10000;

    /// <summary>Symbol theo quy ước của sàn, ví dụ "BTCUSD". Lấy hoàn toàn từ appsettings.json.</summary>
    public List<string> Symbols { get; set; } = [];
}

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
            var candles = klines
                .Select(k => new Candle(key.Exchange, key.Symbol, key.Interval, k.OpenTimeUtc,
                    k.Open, k.High, k.Low, k.Close, k.Volume,
                    isClosed: now >= k.OpenTimeUtc + key.Interval.Duration, receivedAtUtc: now))
                .ToList();

            // Sàn bỏ qua ngày không có giao dịch (Gap) nên chỉ loại nến sai giá trị/thứ tự.
            var batchIssues = CandleSeriesValidator.Validate(candles, key.Interval);
            foreach (var issue in batchIssues.Where(i => i.RejectsCandle))
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
