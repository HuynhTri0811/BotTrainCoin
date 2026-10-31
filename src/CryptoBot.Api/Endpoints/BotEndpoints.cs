using CryptoBot.Application.Abstractions;
using CryptoBot.Application.MarketData;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;
using CryptoBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.Api.Endpoints;

public static class BotEndpoints
{
    // Vertical slice: chỉ phát tín hiệu, môi trường Paper, live bị khóa.
    private const string DecisionMode = "SignalOnly";
    private const string Environment = "Paper";

    public static void MapBotEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/status", async (IStrategyInstanceStore store, MarketDataStatusRegistry registry,
            IClock clock, CancellationToken ct) =>
        {
            var instances = await store.GetAllAsync(ct);
            var feeds = instances
                .Select(instance => new { Key = new CandleKey(instance.Exchange, instance.Symbol,
                    CandleInterval.Parse(instance.Interval)) })
                .DistinctBy(item => item.Key)
                .Select(item => registry.Get(item.Key.ToString(), item.Key.Interval) is { } status
                    ? new FeedStatusDto(status.Key, status.IsStale, status.LastSuccessAtUtc, status.LastAttemptAtUtc,
                        status.LastError,
                        status.ConsecutiveFailures, status.LatestClosedOpenTimeUtc,
                        status.RecentIssues.Select(issue => $"{issue.Kind} {issue.OpenTimeUtc:O}: {issue.Detail}")
                            .ToList())
                    : new FeedStatusDto(item.Key.ToString(), true, null, null, "Chưa đồng bộ lần nào", 0, null, []))
                .ToList();

            return new StatusDto(DecisionMode, Environment, LiveLocked: true, clock.UtcNow, feeds,
                instances.Select(ToDto).ToList());
        });

        api.MapGet("/strategies", async (IStrategyInstanceStore store, CancellationToken ct) =>
            (await store.GetAllAsync(ct)).Select(ToDto));

        api.MapGet("/strategies/{id:int}/chart", async (int id, int? limit, ChartQueryService charts,
            MarketDataOptions options, CancellationToken ct) =>
        {
            var data = await charts.GetAsync(id, limit ?? 300, ct);
            if (data is null)
                return Results.NotFound();

            return Results.Ok(new ChartDto(
                ToDto(data.Instance),
                data.Points.Select(point => ToDto(point.Candle, point.FastEma, point.SlowEma, options)).ToList(),
                data.Signals.Select(ToDto).ToList()));
        });

        // Mới → cũ; lọc theo hành động, tín hiệu backfill và khoảng giờ mở nến [fromUtc, toUtc].
        api.MapGet("/signals", async (int? strategyId, string? action, bool? isBackfill, DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc, int? limit, ISignalStore signals, CancellationToken ct) =>
        {
            if (!QueryEnum.TryParse<SignalAction>(action, out var picked))
                return Results.BadRequest(new { error = QueryEnum.Error<SignalAction>(action!) });

            var filter = new SignalFilter(strategyId, null, picked, isBackfill, fromUtc?.UtcDateTime, toUtc?.UtcDateTime);
            var found = await signals.QueryAsync(filter, Math.Clamp(limit ?? 100, 1, 1000), ct);
            return Results.Ok(found.Select(ToDto).Reverse());
        });

        api.MapGet("/market/candles", async (string symbol, string interval, int? limit, ICandleStore candles,
            MarketDataOptions options, CancellationToken ct) =>
        {
            if (!CandleInterval.TryParse(interval, out var iv))
                return Results.BadRequest(new { error = $"Interval không hỗ trợ: {interval}" });
            if (string.IsNullOrWhiteSpace(symbol) || !symbol.All(char.IsAsciiLetterOrDigit))
                return Results.BadRequest(new { error = "Symbol không hợp lệ" });

            var list = await candles.GetLatestAsync(new CandleKey("Binance", symbol.ToUpperInvariant(), iv), null,
                Math.Clamp(limit ?? 300, 1, 1000), closedOnly: false, ct);
            return Results.Ok(list.Select(candle => ToDto(candle, null, null, options)));
        });

        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        app.MapGet("/health/ready", async (BotDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct)
                ? Results.Ok(new { status = "ready" })
                : Results.Json(new { status = "db-unavailable" }, statusCode: 503));
    }

    private static long ToUnixSeconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();

    private static ChartPointDto ToDto(Candle candle, decimal? fastEma, decimal? slowEma, MarketDataOptions options) =>
        new(ToUnixSeconds(candle.OpenTimeUtc), candle.Open, candle.High, candle.Low, candle.Close, candle.Volume,
            candle.QuoteVolume, candle.TradeCount,
            candle.IsClosed, fastEma, slowEma,
            VolumePressure.From(candle, options.VolumeAlertExcessPercent) is { } pressure
                ? new VolumePressureDto(pressure.BuyVolume, pressure.SellVolume, pressure.BuyPercent,
                    pressure.ExcessPercent, pressure.Side.ToString(),
                    options.VolumeAlertExcessPercent)
                : null);

    private static StrategyDto ToDto(StrategyInstance instance) => new(instance.Id, instance.Name,
        instance.StrategyType, instance.Version,
        instance.Exchange, instance.Symbol, instance.Interval, instance.Enabled,
            instance.LastEvaluatedCandleOpenTimeUtc,
        StrategyFactory.ParseEmaCross(instance.ParametersJson));

    private static SignalDto ToDto(TradingSignal signal) => new(signal.Id, signal.StrategyInstanceId,
        signal.ConfigVersion, signal.Symbol,
        signal.Interval, signal.CandleOpenTimeUtc, ToUnixSeconds(signal.CandleOpenTimeUtc), signal.Action.ToString(),
            signal.Reason,
        signal.CandleClose, signal.FastEma, signal.SlowEma, signal.CreatedAtUtc, signal.IsBackfill);
}

public sealed record StatusDto(string DecisionMode, string Environment, bool LiveLocked, DateTime ServerTimeUtc,
    IReadOnlyList<FeedStatusDto> Feeds, IReadOnlyList<StrategyDto> Strategies);

public sealed record FeedStatusDto(string Key, bool IsStale, DateTime? LastSuccessAtUtc, DateTime? LastAttemptAtUtc,
    string? LastError, int ConsecutiveFailures, DateTime? LatestClosedOpenTimeUtc, IReadOnlyList<string> RecentIssues);

public sealed record StrategyDto(int Id, string Name, string Type, int Version, string Exchange, string Symbol,
    string Interval, bool Enabled, DateTime? LastEvaluatedCandleOpenTimeUtc, EmaCrossParameters Parameters);

public sealed record ChartDto(StrategyDto Strategy, IReadOnlyList<ChartPointDto> Candles, IReadOnlyList<SignalDto> Signals);

/// <summary>
/// Time là Unix seconds (định dạng của Lightweight Charts). Số chỉ để hiển thị.
/// QuoteVolume (USDT), TradeCount và Pressure là null với nến lưu trước khi có các cột này.
/// </summary>
public sealed record ChartPointDto(long Time, decimal Open, decimal High, decimal Low, decimal Close,
    decimal Volume, decimal? QuoteVolume, long? TradeCount, bool IsClosed, decimal? FastEma, decimal? SlowEma,
    VolumePressureDto? Pressure);

/// <summary>Mua/bán chủ động của nến. Side: Balanced | BuyHeavy | SellHeavy so với AlertExcessPercent.</summary>
public sealed record VolumePressureDto(decimal BuyVolume, decimal SellVolume, decimal BuyPercent,
    decimal? ExcessPercent, string Side, decimal AlertExcessPercent);

public sealed record SignalDto(long Id, int StrategyInstanceId, int ConfigVersion, string Symbol, string Interval,
    DateTime CandleOpenTimeUtc, long Time, string Action, string Reason, decimal CandleClose, decimal FastEma,
    decimal SlowEma, DateTime CreatedAtUtc, bool IsBackfill);
