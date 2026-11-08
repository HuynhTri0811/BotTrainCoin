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
                if (await GenerateAsync(symbol, periodEnd, ct))
                    created++;
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
    /// Trả false khi báo cáo đã có; ném lỗi khi sàn không trả nến nào trong khoảng.
    /// </summary>
    public async Task<bool> GenerateAsync(string symbol, DateTime periodEndUtc, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            if (await store.ExistsAsync(symbol, periodEndUtc, ct))
                return false;

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

            store.Add(new DailyReport(symbol, interval.Code, periodStart, periodEndUtc, now,
                JsonSerializer.Serialize(report, Json)));
            await store.SaveAsync(ct);
            logger.LogInformation("Daily report {Symbol} đến {PeriodEnd:O}: {Change:+0.00;-0.00}%, biên độ {Range:0.00}%",
                symbol, periodEndUtc, report.ChangePercent, report.RangePercent);
            return true;
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
                JsonSerializer.Deserialize<DailyMoveReport>(report.ContentJson, Json)!))
            .ToList();
    }
}
