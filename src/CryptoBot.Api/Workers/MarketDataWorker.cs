using CryptoBot.Application.Abstractions;
using CryptoBot.Application.MarketData;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Api.Workers;

/// <summary>
/// Vòng lặp duy nhất: đồng bộ nến qua REST rồi đánh giá chiến lược trên nến đã đóng.
/// Một consumer tuần tự nên không có hai tác vụ cùng ghi một strategy instance.
/// Mỗi vòng lặp dùng DI scope (DbContext) riêng.
/// </summary>
public sealed class MarketDataWorker(
    IServiceScopeFactory scopes,
    MarketDataStatusRegistry status,
    MarketDataOptions options,
    ILogger<MarketDataWorker> logger) : BackgroundService
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = options.PollInterval;
            try
            {
                delay = await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Vòng lặp market data lỗi không mong đợi");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<TimeSpan> RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var instances = await sp.GetRequiredService<IStrategyInstanceStore>().GetAllAsync(ct);
        var sync = sp.GetRequiredService<CandleSyncService>();
        var evaluator = sp.GetRequiredService<SignalEvaluationService>();

        var delay = options.PollInterval;
        var enabled = instances.Where(i => i.Enabled).ToList();

        foreach (var group in enabled.GroupBy(i => new CandleKey(i.Exchange, i.Symbol, CandleInterval.Parse(i.Interval))))
        {
            var key = group.Key;
            try
            {
                var result = await sync.SyncAsync(key, ct);
                status.RecordSuccess(result);
            }
            catch (MarketDataUnavailableException ex)
            {
                var failures = status.RecordFailure(key, ex.Message);
                var backoff = ex.RetryAfter ?? Backoff(failures);
                logger.LogWarning("Sync {Key} thất bại lần {Failures}: {Error}. Thử lại sau {Delay}",
                    key, failures, ex.Message, backoff);
                if (backoff > delay)
                    delay = backoff;
                // Vẫn đánh giá trên các nến đã đóng đã có; dữ liệu stale chỉ chặn entry (giai đoạn sau).
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                status.RecordFailure(key, ex.Message);
                logger.LogError(ex, "Sync {Key} lỗi", key);
            }

            foreach (var instance in group)
            {
                try
                {
                    await evaluator.EvaluatePendingAsync(instance, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Đánh giá strategy {Name} lỗi", instance.Name);
                }
            }
        }

        return delay;
    }

    private TimeSpan Backoff(int failures)
    {
        var exp = options.PollInterval * Math.Pow(2, Math.Min(failures, 10));
        var capped = exp < MaxBackoff ? exp : MaxBackoff;
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
        return capped + jitter;
    }
}
