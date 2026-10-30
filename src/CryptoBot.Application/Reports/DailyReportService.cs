using System.Text.Json;
using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.Reports;

public sealed class DailyReportOptions
{
    /// <summary>Giờ lập báo cáo mỗi ngày, theo <see cref="TimeZone"/>.</summary>
    public TimeSpan RunAt { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Múi giờ IANA của <see cref="RunAt"/>.</summary>
    public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";

    /// <summary>Độ dài khoảng báo cáo, kết thúc đúng lúc lập báo cáo.</summary>
    public TimeSpan Period { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Khung nến Binance dùng để lập báo cáo.</summary>
    public string Interval { get; set; } = "1h";

    /// <summary>Số nến trước mỗi nến dùng làm trung bình khối lượng.</summary>
    public int BaselineCandles { get; set; } = DailyMoveReport.DefaultBaselineCandles;

    /// <summary>Khối lượng gấp ngần này lần trung bình thì coi là bất thường.</summary>
    public decimal VolumeSpikeMultiplier { get; set; } = DailyMoveReport.DefaultVolumeSpikeMultiplier;

    /// <summary>Sàn lỗi thì thử lại sau khoảng này thay vì chờ tới hôm sau.</summary>
    public TimeSpan RetryAfter { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Symbol Binance cần báo cáo, ví dụ "BTCUSDT".</summary>
    public List<string> Symbols { get; set; } = [];
}

public interface IDailyReportStore
{
    Task<bool> ExistsAsync(string symbol, DateTime periodEndUtc, CancellationToken ct);

    /// <summary>Tối đa <paramref name="limit"/> báo cáo mới nhất (theo cuối khoảng), mới → cũ.</summary>
    Task<IReadOnlyList<DailyReport>> GetRecentAsync(string? symbol, int limit, CancellationToken ct);

    void Add(DailyReport report);
    Task SaveAsync(CancellationToken ct);
}

/// <summary>Báo cáo đã lưu kèm nội dung đã đọc lại từ JSON.</summary>
public sealed record SavedDailyReport(string Symbol, string Interval, DateTime CreatedAtUtc, DailyMoveReport Report);

/// <summary>Kết quả một lượt lập báo cáo: số báo cáo mới và lỗi theo symbol (sàn lỗi, chưa có nến…).</summary>
public sealed record DailyReportRun(DateTime PeriodEndUtc, int Created, IReadOnlyList<string> Errors);

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
            var candles = klines.Select(k => k.ToCandle(key, now)).Where(c => c.IsClosed).ToList();
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
            .Select(r => new SavedDailyReport(r.Symbol, r.Interval, r.CreatedAtUtc,
                JsonSerializer.Deserialize<DailyMoveReport>(r.ContentJson, Json)!))
            .ToList();
    }
}
