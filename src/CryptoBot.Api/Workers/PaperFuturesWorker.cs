using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Futures;

namespace CryptoBot.Api.Workers;

/// <summary>Lấy mark price định kỳ rồi kiểm tra thanh lý / SL / TP cho vị thế paper futures.</summary>
public sealed class PaperFuturesWorker(
    IServiceScopeFactory scopes,
    MarkPriceCache cache,
    PaperFuturesOptions options,
    ILogger<PaperFuturesWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = options.MarkPollInterval;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var client = scope.ServiceProvider.GetRequiredService<IMarkPriceClient>();
                foreach (var symbolOptions in options.Symbols)
                {
                    try
                    {
                        cache.Set(await client.GetMarkPriceAsync(symbolOptions.Symbol, stoppingToken));
                    }
                    catch (MarketDataUnavailableException ex)
                    {
                        // Không cập nhật cache: giá cũ sẽ hết hạn và chặn mở/đóng/trigger trên giá stale.
                        logger.LogWarning("Mark price {Symbol}: {Error}", symbolOptions.Symbol, ex.Message);
                        if (ex.RetryAfter is { } ra && ra > delay)
                            delay = ra;
                    }
                }

                await scope.ServiceProvider.GetRequiredService<PaperFuturesService>().EvaluateTriggersAsync(stoppingToken);
                // Sau khi xử lý SL/TP/thanh lý của vị thế đang mở mới đến lượt các quy tắc tự mở lệnh.
                await scope.ServiceProvider.GetRequiredService<AutoOrderService>().EvaluateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Vòng lặp paper futures lỗi");
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
}
