using CryptoBot.Application.Abstractions;
using CryptoBot.Application.MarketData;

namespace CryptoBot.Api.Workers;

/// <summary>Mỗi PollInterval đồng bộ nến ngày lịch sử của từng symbol; lần đầu tải bù từ StartDate.</summary>
public sealed class DailyHistoryWorker(
    IServiceScopeFactory scopes,
    DailyHistoryOptions options,
    ILogger<DailyHistoryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.PollInterval);
        do
        {
            await using var scope = scopes.CreateAsyncScope();
            var sync = scope.ServiceProvider.GetRequiredService<DailyHistorySyncService>();
            foreach (var symbol in options.Symbols)
            {
                try
                {
                    await sync.SyncAsync(symbol, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (MarketDataUnavailableException ex)
                {
                    logger.LogWarning("Daily history {Symbol}: {Error}. Thử lại ở vòng sau", symbol, ex.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Daily history {Symbol} lỗi", symbol);
                }
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
