using System.Text.Json;
using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.Reports;

/// <summary>
/// Lập báo cáo biến động mỗi ngày lúc <see cref="DailyReportOptions.RunAt"/> cho khoảng
/// <see cref="DailyReportOptions.Period"/> vừa qua, từ nến đã đóng lấy thẳng từ Binance. Mỗi (symbol, giờ lập)
/// chỉ có một báo cáo, nên chạy lại hoặc tạo bù sau khi restart không sinh bản trùng.
/// </summary>
public sealed class DailyReportService(
    IMarketDataClient client,
    IDailyReportStore store,
    IClock clock,
    DailyReportOptions options,
    ILogger<DailyReportService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Worker và nút "Tạo ngay" có thể chạy cùng lúc; khóa để không ghi trùng một báo cáo.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Giờ lập báo cáo gần nhất đã tới (≤ <paramref name="nowUtc"/>), UTC.</summary>
    public DateTime LatestRunUtc(DateTime nowUtc)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var run = local.Date + options.RunAt;
        if (run > local)
            run = run.AddDays(-1);
        return TimeZoneInfo.ConvertTimeToUtc(run, tz);
    }

    /// <summary>Giờ lập báo cáo kế tiếp (&gt; <paramref name="nowUtc"/>), UTC.</summary>
    public DateTime NextRunUtc(DateTime nowUtc)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var run = local.Date + options.RunAt;
        if (run <= local)
            run = run.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(run, tz);
    }

    /// <summary>Lập báo cáo còn thiếu cho giờ lập gần nhất của mọi symbol.</summary>
    public async Task<DailyReportRun> RunDueAsync(CancellationToken ct)
    {
        var periodEnd = LatestRunUtc(clock.UtcNow);
        var created = 0;
        var errors = new List<string>();
        foreach (var symbol in options.Symbols)
        {
            try
            {
                var outcome = await GenerateAsync(symbol, periodEnd, ct);
                if (outcome != DailyReportOutcome.AlreadyComplete)
                    created++;
                // Báo cáo thiếu nến được tính là lỗi để worker thử lại sau RetryAfter thay vì đợi đến hôm sau.
                if (outcome == DailyReportOutcome.SavedIncomplete)
                    errors.Add($"{symbol}: sàn mới trả một phần nến của khoảng; báo cáo chưa đủ và sẽ được lập lại.");
            }
            catch (MarketDataUnavailableException ex)
            {
                logger.LogWarning("Daily report {Symbol}: {Error}", symbol, ex.Message);
                errors.Add($"{symbol}: {ex.Message}");
            }
        }

        return new DailyReportRun(periodEnd, created, errors);
    }

    /// <summary>
    /// Lập báo cáo của <paramref name="symbol"/> cho khoảng kết thúc lúc <paramref name="periodEndUtc"/>.
    /// Báo cáo đã đủ nến thì bỏ qua. Sàn mới trả một phần nến (thiếu nến, nến bị loại do sai dữ liệu) thì vẫn lưu
    /// nhưng đánh dấu chưa đủ, và lần gọi sau lập lại cho đến khi đủ. Ném lỗi khi sàn không trả nến nào trong khoảng.
    /// </summary>
    public async Task<DailyReportOutcome> GenerateAsync(string symbol, DateTime periodEndUtc, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var existing = await store.FindAsync(symbol, periodEndUtc, ct);
            if (existing is { IsComplete: true })
                return DailyReportOutcome.AlreadyComplete;

            var interval = CandleInterval.Parse(options.Interval);
            var periodStart = periodEndUtc - options.Period;
            // Lấy thêm BaselineCandles nến trước khoảng để nến đầu khoảng cũng so được khối lượng.
            var fetchFrom = interval.OpenTimeAt(periodStart - interval.Duration * options.BaselineCandles);
            var limit = (int)((periodEndUtc - fetchFrom) / interval.Duration) + 1;
            var klines = await client.GetKlinesAsync(symbol, interval, fetchFrom, limit, ct);

            var now = clock.UtcNow;
            var key = new CandleKey(client.Exchange, symbol, interval);
            var candles = klines.Select(kline => kline.ToCandle(key, now)).Where(candle => candle.IsClosed).ToList();
            candles = CandleSeriesValidator.WithoutRejected(candles, CandleSeriesValidator.Validate(candles, interval));

            var report = DailyMoveReport.Build(candles, periodStart, periodEndUtc, options.BaselineCandles,
                options.VolumeSpikeMultiplier) ?? throw new MarketDataUnavailableException(
                $"Sàn chưa trả nến {interval} nào trong khoảng {periodStart:O} – {periodEndUtc:O}.");

            // Nến đã đóng, hợp lệ, tăng dần và nằm trọn trong khoảng: đủ số lượng nghĩa là liên tục và đủ hai đầu.
            var expected = (int)(options.Period / interval.Duration);
            var complete = report.CandleCount == expected;
            var json = JsonSerializer.Serialize(report, Json);
            if (existing is null)
                store.Add(new DailyReport(symbol, interval.Code, periodStart, periodEndUtc, now, json, complete));
            else
                existing.Refresh(now, json, complete);
            await store.SaveAsync(ct);

            if (!complete)
            {
                logger.LogWarning("Daily report {Symbol} đến {PeriodEnd:O} mới có {Count}/{Expected} nến; sẽ lập lại",
                    symbol, periodEndUtc, report.CandleCount, expected);
                return DailyReportOutcome.SavedIncomplete;
            }

            logger.LogInformation("Daily report {Symbol} đến {PeriodEnd:O}: {Change:+0.00;-0.00}%, biên độ {Range:0.00}%",
                symbol, periodEndUtc, report.ChangePercent, report.RangePercent);
            return DailyReportOutcome.Saved;
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<IReadOnlyList<SavedDailyReport>> GetRecentAsync(string? symbol, int limit, CancellationToken ct)
    {
        var saved = await store.GetRecentAsync(symbol, limit, ct);
        return saved
            .Select(report => new SavedDailyReport(report.Symbol, report.Interval, report.CreatedAtUtc,
                JsonSerializer.Deserialize<DailyMoveReport>(report.ContentJson, Json)!, report.IsComplete))
            .ToList();
    }
}
