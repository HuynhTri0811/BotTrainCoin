using CryptoBot.Application.Abstractions;
using CryptoBot.Application.MarketData;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Tests;

public class MarketDataStatusRegistryTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly CandleKey Key = new("Binance", "BTCUSDT", CandleInterval.H1);

    private readonly FakeClock _clock = new(T0);
    private readonly MarketDataStatusRegistry _registry;

    public MarketDataStatusRegistryTests() =>
        _registry = new MarketDataStatusRegistry(_clock, new MarketDataOptions { StaleAfter = TimeSpan.FromMinutes(2) });

    /// <summary>Nến đóng mới nhất là nến ngay trước nến đang chạy (mở lúc 11:00, đóng lúc 12:00).</summary>
    private static SyncResult Success(DateTime? latestClosed = null, IReadOnlyList<CandleIssue>? issues = null) =>
        new(Key, 1, 0, latestClosed ?? T0.AddHours(-1), T0, issues ?? []);

    private MarketDataStatus Status() => _registry.Get(Key.ToString(), Key.Interval)!;

    [Fact]
    public void Unknown_feed_has_no_status_and_counts_as_stale()
    {
        Assert.Null(_registry.Get(Key.ToString(), Key.Interval));
        Assert.True(_registry.IsStale(Key));
    }

    [Fact]
    public void Fresh_success_is_not_stale()
    {
        _registry.RecordSuccess(Success());

        var s = Status();
        Assert.False(s.IsStale);
        Assert.Equal(T0, s.LastSuccessAtUtc);
        Assert.Equal(T0.AddHours(-1), s.LatestClosedOpenTimeUtc);
        Assert.False(_registry.IsStale(Key));
    }

    [Fact]
    public void Failures_count_up_and_success_resets_them()
    {
        Assert.Equal(1, _registry.RecordFailure(Key, "429"));
        Assert.Equal(2, _registry.RecordFailure(Key, "timeout"));
        Assert.Equal("timeout", Status().LastError);
        Assert.Equal(2, Status().ConsecutiveFailures);

        _registry.RecordSuccess(Success());

        Assert.Equal(0, Status().ConsecutiveFailures);
        Assert.Null(Status().LastError);
    }

    [Fact]
    public void Feed_turns_stale_when_no_success_within_stale_after()
    {
        _registry.RecordSuccess(Success());

        _clock.UtcNow = T0.AddMinutes(2);
        Assert.False(Status().IsStale);

        _clock.UtcNow = T0.AddMinutes(2).AddSeconds(1);
        Assert.True(Status().IsStale);
    }

    [Fact]
    public void Feed_is_stale_when_latest_closed_candle_lags_behind_even_if_sync_succeeds()
    {
        // Nến đóng mới nhất mở lúc 09:00: thiếu nến 10:00 và 11:00.
        _registry.RecordSuccess(Success(latestClosed: T0.AddHours(-3)));

        Assert.True(Status().IsStale);
    }

    [Fact]
    public void Success_without_new_closed_candle_keeps_previous_one()
    {
        _registry.RecordSuccess(Success());

        _registry.RecordSuccess(new SyncResult(Key, 0, 1, null, T0, []));

        Assert.Equal(T0.AddHours(-1), Status().LatestClosedOpenTimeUtc);
    }

    [Fact]
    public void Keeps_only_the_last_20_issues()
    {
        var issues = Enumerable.Range(0, 25)
            .Select(i => new CandleIssue(CandleIssueKind.Gap, T0.AddHours(-i), $"issue {i}"))
            .ToList();

        _registry.RecordSuccess(Success(issues: issues));

        var recent = Status().RecentIssues;
        Assert.Equal(MarketDataStatusRegistry.MaxRecentIssues, recent.Count);
        Assert.Equal("issue 5", recent[0].Detail);
        Assert.Equal("issue 24", recent[^1].Detail);
    }
}
