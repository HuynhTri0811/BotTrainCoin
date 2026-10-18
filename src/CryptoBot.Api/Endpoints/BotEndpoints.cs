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
                .Select(i => new { Key = new CandleKey(i.Exchange, i.Symbol, CandleInterval.Parse(i.Interval)) })
                .DistinctBy(x => x.Key)
                .Select(x => registry.Get(x.Key.ToString(), x.Key.Interval) is { } s
                    ? new FeedStatusDto(s.Key, s.IsStale, s.LastSuccessAtUtc, s.LastAttemptAtUtc, s.LastError,
                        s.ConsecutiveFailures, s.LatestClosedOpenTimeUtc,
                        s.RecentIssues.Select(i => $"{i.Kind} {i.OpenTimeUtc:O}: {i.Detail}").ToList())
                    : new FeedStatusDto(x.Key.ToString(), true, null, null, "Chưa đồng bộ lần nào", 0, null, []))
                .ToList();

            return new StatusDto(DecisionMode, Environment, LiveLocked: true, clock.UtcNow, feeds,
                instances.Select(ToDto).ToList());
        });

        api.MapGet("/strategies", async (IStrategyInstanceStore store, CancellationToken ct) =>
            (await store.GetAllAsync(ct)).Select(ToDto));

        api.MapGet("/strategies/{id:int}/chart", async (int id, int? limit, ChartQueryService charts,
            CancellationToken ct) =>
        {
            var data = await charts.GetAsync(id, limit ?? 300, ct);
            if (data is null)
                return Results.NotFound();

            return Results.Ok(new ChartDto(
                ToDto(data.Instance),
                data.Points.Select(p => new ChartPointDto(
                    ToUnixSeconds(p.Candle.OpenTimeUtc), p.Candle.Open, p.Candle.High, p.Candle.Low,
                    p.Candle.Close, p.Candle.Volume, p.Candle.IsClosed, p.FastEma, p.SlowEma)).ToList(),
                data.Signals.Select(ToDto).ToList()));
        });

        api.MapGet("/signals", async (int? strategyId, int? limit, ISignalStore signals, CancellationToken ct) =>
            (await signals.QueryAsync(strategyId, null, null, null, Math.Clamp(limit ?? 100, 1, 1000), ct))
            .Select(ToDto).Reverse());

        api.MapGet("/market/candles", async (string symbol, string interval, int? limit, ICandleStore candles,
            CancellationToken ct) =>
        {
            if (!CandleInterval.TryParse(interval, out var iv))
                return Results.BadRequest(new { error = $"Interval không hỗ trợ: {interval}" });
            if (string.IsNullOrWhiteSpace(symbol) || !symbol.All(char.IsAsciiLetterOrDigit))
                return Results.BadRequest(new { error = "Symbol không hợp lệ" });

            var list = await candles.GetLatestAsync(new CandleKey("Binance", symbol.ToUpperInvariant(), iv), null,
                Math.Clamp(limit ?? 300, 1, 1000), closedOnly: false, ct);
            return Results.Ok(list.Select(c => new ChartPointDto(ToUnixSeconds(c.OpenTimeUtc), c.Open, c.High,
                c.Low, c.Close, c.Volume, c.IsClosed, null, null)));
        });

        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        app.MapGet("/health/ready", async (BotDbContext db, CancellationToken ct) =>
            await db.Database.CanConnectAsync(ct)
                ? Results.Ok(new { status = "ready" })
                : Results.Json(new { status = "db-unavailable" }, statusCode: 503));
    }

    private static long ToUnixSeconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();

    private static StrategyDto ToDto(StrategyInstance i) => new(i.Id, i.Name, i.StrategyType, i.Version,
        i.Exchange, i.Symbol, i.Interval, i.Enabled, i.LastEvaluatedCandleOpenTimeUtc,
        StrategyFactory.ParseEmaCross(i.ParametersJson));

    private static SignalDto ToDto(TradingSignal s) => new(s.Id, s.StrategyInstanceId, s.ConfigVersion, s.Symbol,
        s.Interval, s.CandleOpenTimeUtc, ToUnixSeconds(s.CandleOpenTimeUtc), s.Action.ToString(), s.Reason,
        s.CandleClose, s.FastEma, s.SlowEma, s.CreatedAtUtc, s.IsBackfill);
}

public sealed record StatusDto(string DecisionMode, string Environment, bool LiveLocked, DateTime ServerTimeUtc,
    IReadOnlyList<FeedStatusDto> Feeds, IReadOnlyList<StrategyDto> Strategies);

public sealed record FeedStatusDto(string Key, bool IsStale, DateTime? LastSuccessAtUtc, DateTime? LastAttemptAtUtc,
    string? LastError, int ConsecutiveFailures, DateTime? LatestClosedOpenTimeUtc, IReadOnlyList<string> RecentIssues);

public sealed record StrategyDto(int Id, string Name, string Type, int Version, string Exchange, string Symbol,
    string Interval, bool Enabled, DateTime? LastEvaluatedCandleOpenTimeUtc, EmaCrossParameters Parameters);

public sealed record ChartDto(StrategyDto Strategy, IReadOnlyList<ChartPointDto> Candles, IReadOnlyList<SignalDto> Signals);

/// <summary>Time là Unix seconds (định dạng của Lightweight Charts). Số chỉ để hiển thị.</summary>
public sealed record ChartPointDto(long Time, decimal Open, decimal High, decimal Low, decimal Close,
    decimal Volume, bool IsClosed, decimal? FastEma, decimal? SlowEma);

public sealed record SignalDto(long Id, int StrategyInstanceId, int ConfigVersion, string Symbol, string Interval,
    DateTime CandleOpenTimeUtc, long Time, string Action, string Reason, decimal CandleClose, decimal FastEma,
    decimal SlowEma, DateTime CreatedAtUtc, bool IsBackfill);
