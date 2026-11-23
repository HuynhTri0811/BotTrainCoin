using System.Collections.Concurrent;
using CryptoBot.Application.Abstractions;

namespace CryptoBot.Application.MarketData;

/// <summary>
/// Singleton ghi nhớ các lần tải lại khoảng nến thiếu, để một khoảng thiếu vĩnh viễn trên sàn chỉ được thử lại một
/// số lần có giới hạn và cách nhau một khoảng, thay vì mỗi lần sync.
/// </summary>
public sealed class CandleGapTracker
{
    private readonly ConcurrentDictionary<(CandleKey Key, DateTime GapStartUtc), Attempts> _attempts = new();

    /// <summary>Có nên tải lại khoảng thiếu bắt đầu ở <paramref name="gapStartUtc"/> lúc này không; có thì ghi một lần thử.</summary>
    public bool TryBeginRepair(CandleKey key, DateTime gapStartUtc, DateTime nowUtc, MarketDataOptions options)
    {
        Attempts previous = _attempts.GetValueOrDefault((key, gapStartUtc));
        if (previous.Count >= options.MaxGapRepairAttempts || nowUtc < previous.NextAttemptUtc)
            return false;
        _attempts[(key, gapStartUtc)] = new Attempts(previous.Count + 1, nowUtc + options.GapRepairRetryInterval);
        return true;
    }

    /// <summary>Khoảng thiếu đã được lấp: quên số lần thử.</summary>
    public void Resolved(CandleKey key, DateTime gapStartUtc) => _attempts.TryRemove((key, gapStartUtc), out _);

    private readonly record struct Attempts(int Count, DateTime NextAttemptUtc);
}
