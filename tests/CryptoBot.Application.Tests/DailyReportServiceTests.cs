using System.Globalization;
using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Reports;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.Application.Tests;

public class DailyReportServiceTests
{
    // 06:00 ngày 30/10 giờ Việt Nam (UTC+7).
    private static readonly DateTime RunUtc = new(2026, 10, 29, 23, 0, 0, DateTimeKind.Utc);

    private readonly FakeClock _clock = new(RunUtc);
    private readonly InMemoryDailyReportStore _store = new();
    private readonly FakeMarketDataClient _client;
    private readonly DailyReportService _service;

    public DailyReportServiceTests()
    {
        _client = new FakeMarketDataClient(_clock);
        _service = new DailyReportService(_client, _store, _clock,
            new DailyReportOptions { Symbols = ["BTCUSDT"] }, NullLogger<DailyReportService>.Instance);
    }

    /// <summary>Nến 1h đi lên đều 1 đơn vị mỗi giờ, từ <paramref name="hoursBack"/> giờ trước giờ lập.</summary>
    private void AddRisingKlines(int hoursBack)
    {
        for (var i = 0; i < hoursBack; i++)
        {
            var open = 100m + i;
            _client.Klines.Add(new KlineDto(RunUtc.AddHours(i - hoursBack), open, open + 1, open, open + 1, 10,
                TakerBuyVolume: 6));
        }
    }

    [Theory]
    [InlineData("2026-10-29T22:59:59Z", "2026-10-28T23:00:00Z")]
    [InlineData("2026-10-29T23:00:00Z", "2026-10-29T23:00:00Z")]
    [InlineData("2026-10-30T10:00:00Z", "2026-10-29T23:00:00Z")]
    public void Latest_run_is_6am_vietnam_time_that_has_already_passed(string now, string expected)
    {
        Assert.Equal(Utc(expected), _service.LatestRunUtc(Utc(now)));
    }

    private static DateTime Utc(string iso) =>
        DateTime.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

    [Fact]
    public void Next_run_is_the_following_6am_vietnam_time()
    {
        Assert.Equal(RunUtc, _service.NextRunUtc(RunUtc.AddSeconds(-1)));
        Assert.Equal(RunUtc.AddDays(1), _service.NextRunUtc(RunUtc));
    }

    [Fact]
    public async Task Builds_the_report_for_the_24_hours_before_the_run()
    {
        AddRisingKlines(50);

        var run = await _service.RunDueAsync(default);

        Assert.Equal(1, run.Created);
        Assert.Empty(run.Errors);
        var saved = Assert.Single(_store.Reports);
        Assert.Equal(RunUtc.AddHours(-24), saved.PeriodStartUtc);
        Assert.Equal(RunUtc, saved.PeriodEndUtc);
        Assert.Equal("1h", saved.Interval);

        var report = Assert.Single(await _service.GetRecentAsync("BTCUSDT", 10, default)).Report;
        Assert.Equal(24, report.CandleCount);
        Assert.Equal(126m, report.Open); // nến thứ 26 trong 50 nến
        Assert.Equal(150m, report.Close);
        Assert.Equal(60m, report.BuyPercent);
        // Nến đầu khoảng cũng có đủ 20 nến trước để so khối lượng.
        Assert.Equal(1m, report.BiggestRise.VolumeRatio);
    }

    [Fact]
    public async Task Running_again_does_not_fetch_or_save_twice()
    {
        AddRisingKlines(50);
        await _service.RunDueAsync(default);

        var again = await _service.RunDueAsync(default);

        Assert.Equal(0, again.Created);
        Assert.Single(_store.Reports);
        Assert.Equal(1, _client.Calls);
    }

    [Fact]
    public async Task Missed_run_is_made_up_later_the_same_day()
    {
        AddRisingKlines(50);
        // Bot tắt lúc 06:00, bật lại lúc 09:30 giờ Việt Nam.
        _clock.UtcNow = RunUtc.AddHours(3.5);

        await _service.RunDueAsync(default);

        var saved = Assert.Single(_store.Reports);
        Assert.Equal(RunUtc, saved.PeriodEndUtc);
        Assert.Equal(RunUtc.AddHours(3.5), saved.CreatedAtUtc);
    }

    [Fact]
    public async Task No_candles_is_reported_as_an_error_and_nothing_is_saved()
    {
        var run = await _service.RunDueAsync(default);

        Assert.Equal(0, run.Created);
        Assert.Contains("BTCUSDT", Assert.Single(run.Errors));
        Assert.Empty(_store.Reports);
    }
}
