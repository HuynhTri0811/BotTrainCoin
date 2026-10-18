using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.Signals;

public sealed class SignalOptions
{
    /// <summary>
    /// Signal được coi là mới (không phải backfill) nếu được tạo trong khoảng này sau khi nến đóng.
    /// Chỉ signal mới mới có thể được dùng để đặt lệnh ở các giai đoạn sau.
    /// </summary>
    public TimeSpan MaxFreshAge { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Số nến đóng tối đa đánh giá mỗi lần chạy.</summary>
    public int MaxCandlesPerRun { get; set; } = 2000;
}

public sealed record EvaluationResult(int Evaluated, IReadOnlyList<TradingSignal> NewSignals);

/// <summary>
/// Đánh giá từng nến đã đóng sau checkpoint, theo thứ tự, mỗi nến đúng một lần cho mỗi
/// strategy instance/config version. Signal và checkpoint được ghi trong cùng một transaction.
/// </summary>
public sealed class SignalEvaluationService(
    ICandleStore candles,
    ISignalStore signals,
    IClock clock,
    SignalOptions options,
    ILogger<SignalEvaluationService> logger)
{
    public async Task<EvaluationResult> EvaluatePendingAsync(StrategyInstance instance, CancellationToken ct)
    {
        var strategy = StrategyFactory.Create(instance);
        var interval = CandleInterval.Parse(instance.Interval);
        var key = new CandleKey(instance.Exchange, instance.Symbol, interval);

        var pending = await candles.GetAfterAsync(key, instance.LastEvaluatedCandleOpenTimeUtc,
            options.MaxCandlesPerRun, closedOnly: true, ct);
        if (pending.Count == 0)
            return new EvaluationResult(0, []);

        var history = await candles.GetLatestAsync(key, pending[0].OpenTimeUtc,
            strategy.RequiredCandles - 1, closedOnly: true, ct);
        var series = history.Concat(pending).ToList();

        // Trong MVP signal-only chưa có portfolio: coi BUY gần nhất chưa có SELL là "đang giữ".
        // Khi có Portfolio, giá trị này phải lấy từ vị thế đã khớp thực tế.
        var last = await signals.GetLastAsync(instance.Id, instance.Version, ct);
        var hasPosition = last?.Action == SignalAction.Buy;

        var now = clock.UtcNow;
        var created = new List<TradingSignal>();

        for (var i = history.Count; i < series.Count; i++)
        {
            var candle = series[i];
            var start = Math.Max(0, i - strategy.RequiredCandles + 1);
            var window = series.GetRange(start, i - start + 1);

            var decision = HasGap(window, interval)
                ? StrategyDecision.Hold("Cửa sổ nến có khoảng thiếu; bỏ qua đánh giá.")
                : strategy.Evaluate(new StrategyContext(window, hasPosition, HasPendingEntry: false, instance.Version));

            if (decision.Action != SignalAction.Hold && decision.Indicators is { } ind)
            {
                var isBackfill = now - candle.CloseTimeUtc > options.MaxFreshAge;
                created.Add(new TradingSignal(instance.Id, instance.Version, instance.Symbol, instance.Interval,
                    candle.OpenTimeUtc, decision.Action, decision.Reason, candle.Close,
                    ind.Fast, ind.Slow, now, isBackfill));
                hasPosition = decision.Action == SignalAction.Buy;
            }

            instance.MarkEvaluated(candle.OpenTimeUtc);
        }

        await signals.SaveEvaluationAsync(instance, created, ct);

        foreach (var s in created)
            logger.LogInformation(
                "Signal {Action} {Symbol} {Interval} nến {OpenTime:O} close {Close} ({Reason}) backfill={Backfill} instance={InstanceId} v{Version}",
                s.Action, s.Symbol, s.Interval, s.CandleOpenTimeUtc, s.CandleClose, s.Reason, s.IsBackfill,
                s.StrategyInstanceId, s.ConfigVersion);

        return new EvaluationResult(pending.Count, created);
    }

    private static bool HasGap(List<Candle> window, CandleInterval interval) =>
        window.Count > 1 &&
        window[^1].OpenTimeUtc - window[0].OpenTimeUtc != interval.Duration * (window.Count - 1);
}
