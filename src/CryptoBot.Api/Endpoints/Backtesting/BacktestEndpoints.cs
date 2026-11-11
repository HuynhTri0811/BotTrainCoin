using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Backtesting;
using CryptoBot.Application.MarketData;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Backtest chiến lược trên nến lịch sử của một strategy instance. Chỉ là mô phỏng: không đặt lệnh nào, kể cả Paper.
/// </summary>
public static class BacktestEndpoints
{
    public static void MapBacktestEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/strategies/{id:int}/backtest");

        api.MapGet("/coverage", async (int id, BacktestService backtests, CancellationToken ct) =>
            await backtests.GetCoverageAsync(id, ct) is { } coverage
                ? Results.Ok(new BacktestCoverageDto(coverage.FirstOpenTimeUtc, coverage.LastOpenTimeUtc))
                : Results.NotFound(new { error = "Không có chiến lược này." }));

        // Tải nến cũ hơn nến sớm nhất đã lưu, để backtest có thêm lịch sử. Có thể mất vài phút với nến 15m.
        api.MapPost("/history", async (int id, BackfillRequestDto body, IStrategyInstanceStore instances,
            CandleBackfillService backfill, IClock clock, CancellationToken ct) =>
        {
            if (await instances.GetAsync(id, ct) is not { } instance)
                return Results.NotFound(new { error = "Không có chiến lược này." });
            var from = body.FromUtc.UtcDateTime;
            if (from >= clock.UtcNow)
                return Results.BadRequest(new { error = "Ngày bắt đầu phải ở quá khứ." });

            var key = new CandleKey(instance.Exchange, instance.Symbol, CandleInterval.Parse(instance.Interval));
            try
            {
                return await backfill.BackfillAsync(key, from, ct) is { } result
                    ? Results.Ok(new BackfillDto(result.Inserted, result.Requests, result.RejectedCandles,
                        result.FirstOpenTimeUtc, result.ReachedRequestLimit))
                    : Results.Conflict(new { error = "Đang tải lịch sử ở một yêu cầu khác, thử lại sau." });
            }
            catch (MarketDataUnavailableException ex)
            {
                return Results.Json(new { error = $"Sàn tạm thời không trả dữ liệu: {ex.Message}" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        api.MapPost("/", async (int id, BacktestRequestDto body, IStrategyInstanceStore instances,
            BacktestService backtests, CancellationToken ct) =>
        {
            if (await instances.GetAsync(id, ct) is not { } instance)
                return Results.NotFound(new { error = "Không có chiến lược này." });
            if (body.ToRequest(id, StrategyFactory.ParseEmaCross(instance.ParametersJson)) is not { } request)
                return Results.BadRequest(new { error = QueryEnum.Error<PositionSizing>(body.Sizing ?? "") });

            try
            {
                return await backtests.RunAsync(request, ct) is { } run
                    ? Results.Ok(ToDto(run))
                    : Results.NotFound(new { error = "Không có chiến lược này." });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }

    private static BacktestDto ToDto(BacktestRun run)
    {
        var result = run.Result;
        var statistics = result.Statistics;
        return new BacktestDto(
            run.Instance.Id, run.Instance.Symbol, run.Instance.Interval, run.Parameters,
            run.FirstCandleUtc, run.LastCandleUtc, run.CandleCount, run.WarmupCandles,
            result.EvaluatedCandles, result.SkippedGapWindows,
            result.Signals.Count(signal => signal.Action == SignalAction.Buy),
            result.Signals.Count(signal => signal.Action == SignalAction.Sell),
            new BacktestStatisticsDto(statistics.TotalTrades, statistics.Wins, statistics.Losses,
                statistics.WinRatePercent, statistics.NetProfit, statistics.NetProfitPercent, statistics.GrossProfit,
                statistics.GrossLoss, statistics.ProfitFactor, statistics.TotalFees, statistics.MaxDrawdown,
                statistics.MaxDrawdownPercent, statistics.EndingEquity, statistics.BuyAndHoldPercent),
            result.Trades.Select(trade => new BacktestTradeDto(trade.EntryTimeUtc, trade.EntryPrice,
                trade.ExitTimeUtc, trade.ExitPrice, trade.Quantity, trade.Fees, trade.NetPnl, trade.ReturnPercent,
                trade.ExitReason.ToString())).ToList(),
            result.OpenTrade is { } open
                ? new BacktestOpenTradeDto(open.EntryTimeUtc, open.EntryPrice, open.Quantity, open.EntryFee,
                    open.MarkPrice, open.UnrealizedPnl)
                : null,
            new BacktestChartDto(run.Chart.CandlesPerBar,
                run.Chart.Bars.Select(bar => new BacktestBarDto(ToUnixSeconds(bar.OpenTimeUtc), bar.Open, bar.High,
                    bar.Low, bar.Close, bar.Equity)).ToList(),
                run.Chart.Markers.Select(marker => new BacktestMarkerDto(ToUnixSeconds(marker.BarOpenTimeUtc),
                    marker.IsEntry ? "Entry" : "Exit", marker.Price, marker.ExitReason?.ToString(),
                    marker.NetPnl)).ToList()));
    }

    private static long ToUnixSeconds(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds();
}
