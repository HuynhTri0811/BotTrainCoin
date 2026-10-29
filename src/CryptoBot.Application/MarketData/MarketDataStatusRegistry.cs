using System.Collections.Concurrent;
using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.MarketData;

public sealed record MarketDataStatus(
    string Key,
    DateTime? LastSuccessAtUtc,
    DateTime? LastAttemptAtUtc,
    string? LastError,
    int ConsecutiveFailures,
    DateTime? LatestClosedOpenTimeUtc,
    IReadOnlyList<CandleIssue> RecentIssues,
    bool IsStale);

/// <summary>Trạng thái đồng bộ trong bộ nhớ cho /api/status. Không phải nguồn dữ liệu bền vững.</summary>
public sealed class MarketDataStatusRegistry(IClock clock, MarketDataOptions options)
{
    /// <summary>Số lỗi dữ liệu gần nhất giữ lại cho mỗi feed.</summary>
    public const int MaxRecentIssues = 20;

    private sealed class Entry
    {
        public DateTime? LastSuccessAtUtc;
        public DateTime? LastAttemptAtUtc;
        public string? LastError;
        public int ConsecutiveFailures;
        public DateTime? LatestClosedOpenTimeUtc;
        public IReadOnlyList<CandleIssue> RecentIssues = [];
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public void RecordSuccess(SyncResult result)
    {
        var e = _entries.GetOrAdd(result.Key.ToString(), _ => new Entry());
        lock (e)
        {
            e.LastAttemptAtUtc = e.LastSuccessAtUtc = clock.UtcNow;
            e.LastError = null;
            e.ConsecutiveFailures = 0;
            e.LatestClosedOpenTimeUtc = result.LatestClosedOpenTimeUtc ?? e.LatestClosedOpenTimeUtc;
            if (result.Issues.Count > 0)
                e.RecentIssues = result.Issues.TakeLast(MaxRecentIssues).ToList();
        }
    }

    public int RecordFailure(CandleKey key, string error)
    {
        var e = _entries.GetOrAdd(key.ToString(), _ => new Entry());
        lock (e)
        {
            e.LastAttemptAtUtc = clock.UtcNow;
            e.LastError = error;
            return ++e.ConsecutiveFailures;
        }
    }

    public bool IsStale(CandleKey key) => Get(key.ToString(), key.Interval)?.IsStale ?? true;

    public MarketDataStatus? Get(string key, CandleInterval interval)
    {
        if (!_entries.TryGetValue(key, out var e))
            return null;

        lock (e)
        {
            var now = clock.UtcNow;
            var syncStale = e.LastSuccessAtUtc is not { } ok || now - ok > options.StaleAfter;
            // Nến đóng mới nhất phải là nến ngay trước nến đang chạy (cho phép trễ bằng StaleAfter).
            var candleStale = e.LatestClosedOpenTimeUtc is not { } lc ||
                              now - (lc + interval.Duration) > interval.Duration + options.StaleAfter;

            return new MarketDataStatus(key, e.LastSuccessAtUtc, e.LastAttemptAtUtc, e.LastError,
                e.ConsecutiveFailures, e.LatestClosedOpenTimeUtc, e.RecentIssues, syncStale || candleStale);
        }
    }
}
