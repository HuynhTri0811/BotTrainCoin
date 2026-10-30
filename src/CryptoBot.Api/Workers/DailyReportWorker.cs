using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Reports;

namespace CryptoBot.Api.Workers;

/// <summary>
/// Lập báo cáo biến động mỗi ngày lúc <see cref="DailyReportOptions.RunAt"/>. Khi khởi động thì tạo bù báo cáo
/// còn thiếu của giờ lập gần nhất; sàn lỗi thì thử lại sau <see cref="DailyReportOptions.RetryAfter"/>.
/// </summary>
public sealed class DailyReportWorker(
    IServiceScopeFactory scopes,
    IClock clock,
    DailyReportOptions options,
    ILogger<DailyReportWorker> logger) : BackgroundService
{
    // Ngủ tối đa ngần này rồi kiểm tra lại, để máy ngủ/thức (Task.Delay bị trễ) vẫn tạo bù được báo cáo.
    private static readonly TimeSpan MaxSleep = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime wakeAt;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<DailyReportService>();
                var run = await service.RunDueAsync(stoppingToken);
                var now = clock.UtcNow;
                wakeAt = service.NextRunUtc(now);
                if (run.Errors.Count > 0 && now + options.RetryAfter < wakeAt)
                    wakeAt = now + options.RetryAfter;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Daily report lỗi");
                wakeAt = clock.UtcNow + options.RetryAfter;
            }

            var sleep = wakeAt - clock.UtcNow;
            try
            {
                await Task.Delay(sleep < TimeSpan.Zero ? TimeSpan.Zero : sleep < MaxSleep ? sleep : MaxSleep,
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
