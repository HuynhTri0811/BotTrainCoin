using CryptoBot.Application.LivePrices;

namespace CryptoBot.Api.Workers;

/// <summary>Mỗi PollInterval lấy giá Spot + Mark của từng symbol và ghi vào lịch sử.</summary>
public sealed class LivePriceWorker(
    IServiceScopeFactory scopes,
    LivePriceOptions options,
    ILogger<LivePriceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Căn theo chu kỳ cố định (không cộng dồn thời gian xử lý) để mốc 15s đều đặn.
        using var timer = new PeriodicTimer(options.PollInterval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sampler = scope.ServiceProvider.GetRequiredService<LivePriceSampler>();
                foreach (var symbol in options.Symbols)
                {
                    var sample = await sampler.SampleAsync(symbol, stoppingToken);
                    if (sample.Error is not null)
                        logger.LogWarning("Live price {Symbol}: {Error}", symbol, sample.Error);
                    else
                        logger.LogInformation("Live price {Symbol}: spot {Spot} mark {Mark} ({SpotMs}ms/{MarkMs}ms)",
                            symbol, sample.SpotPrice, sample.MarkPrice, sample.SpotLatencyMs, sample.MarkLatencyMs);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Vòng lặp live price lỗi");
            }
        } while (await WaitNextAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitNextAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
