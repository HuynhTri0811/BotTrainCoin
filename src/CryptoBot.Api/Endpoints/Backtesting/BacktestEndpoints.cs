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

            var key = CandleKey.For(instance);
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

        // Mọi lần chạy được lưu vào lịch sử để mở lại và so sánh.
        api.MapPost("/", async (int id, BacktestRequestDto body, BacktestService backtests,
            BacktestHistoryService history, CancellationToken ct) =>
        {
            if (body.ToRequest(id) is not { } request)
                return Results.BadRequest(new { error = QueryEnum.Error<PositionSizing>(body.Sizing ?? "") });

            try
            {
                if (await backtests.RunAsync(request, ct) is not { } run)
                    return Results.NotFound(new { error = "Không có chiến lược này." });
                return Results.Ok(ToDto(await history.SaveAsync(run, ct)));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }

    /// <summary>Lịch sử các lần backtest đã lưu, cho mọi chiến lược.</summary>
    public static void MapBacktestHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/backtests");

        api.MapGet("/", async (int? strategyId, int? limit, int? offset, BacktestHistoryService history,
            CancellationToken ct) =>
            (await history.ListAsync(strategyId, limit ?? 100, offset ?? 0, ct)).Select(ToDto));

        // Tổng số lần chạy: danh sách chỉ trả một trang, số này cho biết còn bao nhiêu chưa hiện.
        api.MapGet("/count", async (int? strategyId, BacktestHistoryService history, CancellationToken ct) =>
            new BacktestCountDto(await history.CountAsync(strategyId, ct)));

        api.MapGet("/{backtestId:long}", async (long backtestId, BacktestHistoryService history,
            CancellationToken ct) =>
            await history.GetAsync(backtestId, ct) is { } saved
                ? Results.Ok(ToDto(saved))
                : Results.NotFound(new { error = "Không có lần backtest này." }));

        api.MapPut("/{backtestId:long}/note", async (long backtestId, BacktestNoteDto body,
            BacktestHistoryService history, CancellationToken ct) =>
        {
            try
            {
                return await history.SetNoteAsync(backtestId, body.Note, ct) is { } saved
                    ? Results.Ok(new BacktestNoteDto(saved.Note))
                    : Results.NotFound(new { error = "Không có lần backtest này." });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        api.MapDelete("/{backtestId:long}", async (long backtestId, BacktestHistoryService history,
            CancellationToken ct) =>
            await history.DeleteAsync(backtestId, ct)
                ? Results.NoContent()
                : Results.NotFound(new { error = "Không có lần backtest này." }));
    }

    private static BacktestSettingsDto ToDto(BacktestSettings settings) => new(settings.InitialCapital,
        settings.Sizing.ToString(), settings.SizeValue, settings.TakeProfitPercent, settings.StopLossPercent,
        settings.FeePercent);

    private static BacktestSummaryDto ToDto(BacktestSummary summary) => new(summary.Id, summary.CreatedAtUtc,
        summary.StrategyInstanceId, summary.Symbol, summary.Interval,
        new EmaCrossParameters(summary.FastPeriod, summary.SlowPeriod, summary.LookbackCandles),
        summary.FirstCandleUtc, summary.LastCandleUtc, ToDto(summary.Settings), summary.TotalTrades,
        summary.WinRatePercent, summary.NetProfit, summary.NetProfitPercent, summary.MaxDrawdownPercent, summary.Note);

    private static BacktestDto ToDto(SavedBacktestRun savedRun)
    {
        var saved = savedRun.Saved;
        var snapshot = savedRun.Snapshot;
        var statistics = snapshot.Statistics;
        return new BacktestDto(
            saved.Id, saved.CreatedAtUtc, saved.Note,
            saved.StrategyInstanceId, saved.Symbol, saved.Interval, snapshot.Parameters, ToDto(snapshot.Settings),
            snapshot.FirstCandleUtc, snapshot.LastCandleUtc, snapshot.CandleCount, snapshot.WarmupCandles,
            snapshot.EvaluatedCandles, snapshot.SkippedGapWindows, snapshot.BuySignals, snapshot.SellSignals,
            new BacktestStatisticsDto(statistics.TotalTrades, statistics.Wins, statistics.Losses,
                statistics.WinRatePercent, statistics.NetProfit, statistics.NetProfitPercent, statistics.GrossProfit,
                statistics.GrossLoss, statistics.ProfitFactor, statistics.TotalFees, statistics.MaxDrawdown,
                statistics.MaxDrawdownPercent, statistics.EndingEquity, statistics.BuyAndHoldPercent),
            snapshot.Trades.Select(trade => new BacktestTradeDto(trade.EntryTimeUtc, trade.EntryPrice,
                trade.ExitTimeUtc, trade.ExitPrice, trade.Quantity, trade.Fees, trade.NetPnl, trade.ReturnPercent,
                trade.ExitReason.ToString())).ToList(),
            snapshot.OpenTrade is { } open
                ? new BacktestOpenTradeDto(open.EntryTimeUtc, open.EntryPrice, open.Quantity, open.EntryFee,
                    open.MarkPrice, open.UnrealizedPnl)
                : null,
            new BacktestChartDto(snapshot.Chart.CandlesPerBar,
                snapshot.Chart.Bars.Select(bar => new BacktestBarDto(ToUnixSeconds(bar.OpenTimeUtc), bar.Open,
                    bar.High, bar.Low, bar.Close, bar.Equity)).ToList(),
                snapshot.Chart.Markers.Select(marker => new BacktestMarkerDto(ToUnixSeconds(marker.BarOpenTimeUtc),
                    marker.IsEntry ? "Entry" : "Exit", marker.Price, marker.ExitReason?.ToString(),
                    marker.NetPnl)).ToList()));
    }

    private static long ToUnixSeconds(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds();
}
