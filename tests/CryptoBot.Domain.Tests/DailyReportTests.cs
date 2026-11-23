using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

public class DailyReportTests
{
    private static readonly DateTime End = new(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Incomplete_report_can_be_refreshed_until_complete()
    {
        DailyReport report = new("BTCUSDT", "1h", End.AddDays(-1), End, End, "{\"candleCount\":23}", isComplete: false);

        report.Refresh(End.AddHours(1), "{\"candleCount\":24}", isComplete: true);

        Assert.True(report.IsComplete);
        Assert.Equal(End.AddHours(1), report.CreatedAtUtc);
        Assert.Equal("{\"candleCount\":24}", report.ContentJson);
    }

    [Fact]
    public void Complete_report_is_never_rewritten()
    {
        DailyReport report = new("BTCUSDT", "1h", End.AddDays(-1), End, End, "{}");

        Assert.Throws<InvalidOperationException>(() => report.Refresh(End.AddHours(1), "{}", isComplete: true));
    }
}
