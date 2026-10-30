using CryptoBot.Application.News;

namespace CryptoBot.Api.Workers;

/// <summary>Đồng bộ tin tức từ các nguồn RSS ngay khi khởi động, sau đó mỗi PollInterval (mặc định 5 giờ).</summary>
public sealed class NewsWorker(
    IServiceScopeFactory scopes,
    NewsOptions options,
    ILogger<NewsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.PollInterval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<NewsSyncService>().SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "News lỗi");
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
