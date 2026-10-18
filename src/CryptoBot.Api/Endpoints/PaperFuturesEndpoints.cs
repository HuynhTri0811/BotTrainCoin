using CryptoBot.Application.Futures;
using CryptoBot.Domain.Futures;

namespace CryptoBot.Api.Endpoints;

/// <summary>
/// Paper futures. Môi trường nằm ngay trong đường dẫn (/api/paper/...): không có cách nào
/// để các endpoint này chạm vào tài khoản thật.
/// </summary>
public static class PaperFuturesEndpoints
{
    public static void MapPaperFuturesEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/paper/futures");

        api.MapGet("/account", async (PaperFuturesService svc, PaperFuturesOptions options, CancellationToken ct) =>
        {
            var a = await svc.GetAccountAsync(ct);
            return new AccountDto("Paper", a.WalletBalance, a.UsedMargin, a.UnrealizedPnl, a.Equity, a.AvailableBalance,
                a.OpenPositions.Select(v => ToDto(v.Position, v.MarkPrice, v.UnrealizedPnl, v.RoePercent)).ToList(),
                a.MarkPrices.Select(m => new MarkPriceDto(m.Symbol, m.Price, m.TimeUtc)).ToList(),
                new RulesDto(options.MaxLeverage, options.TakerFeeRate, options.SlippageRate,
                    options.Symbols.Select(s => s.Symbol).ToList()));
        });

        api.MapPost("/quote", async (OpenRequestDto body, PaperFuturesService svc, CancellationToken ct) =>
            body.ToRequest() is { } req
                ? Results.Ok(ToDto(await svc.QuoteAsync(req, ct)))
                : Results.BadRequest(new { error = "Side phải là Long hoặc Short." }));

        api.MapPost("/orders", async (OpenRequestDto body, PaperFuturesService svc, CancellationToken ct) =>
        {
            if (body.CommandId == Guid.Empty)
                return Results.BadRequest(new { error = "Thiếu commandId." });
            if (body.ToRequest() is not { } req)
                return Results.BadRequest(new { error = "Side phải là Long hoặc Short." });

            var result = await svc.OpenAsync(req, ct);
            return result.Position is { } p
                ? Results.Ok(new OpenResponseDto(true, result.Duplicate, [], ToDto(p, null, null, null)))
                : Results.UnprocessableEntity(new OpenResponseDto(false, false, result.Quote.Errors, null));
        });

        api.MapPost("/positions/{id:long}/close", async (long id, CloseRequestDto body, PaperFuturesService svc,
            CancellationToken ct) =>
        {
            if (body.CommandId == Guid.Empty)
                return Results.BadRequest(new { error = "Thiếu commandId." });

            var result = await svc.CloseAsync(id, body.CommandId, ct);
            return result.Ok
                ? Results.Ok(ToDto(result.Position!, null, null, null))
                : Results.UnprocessableEntity(new { error = result.Error });
        });

        api.MapGet("/positions/history", async (int? limit, IPaperFuturesStore store, CancellationToken ct) =>
            (await store.GetClosedAsync(Math.Clamp(limit ?? 50, 1, 500), ct))
            .Select(p => ToDto(p, null, null, null)));
    }

    private static PositionDto ToDto(FuturesPosition p, decimal? mark, decimal? upnl, decimal? roe) => new(
        p.Id, p.Environment, p.Symbol, p.Side.ToString(), p.Leverage, p.Quantity, p.EntryPrice, p.Margin,
        p.LiquidationPrice, p.StopLoss, p.TakeProfit, p.OpenFee, p.CloseFee, p.Status.ToString(),
        p.CloseReason?.ToString(), p.ExitPrice, p.RealizedPnl, p.Status == PositionStatus.Open ? null : p.NetPnl,
        p.OpenedAtUtc, p.ClosedAtUtc, mark, upnl, roe);

    private static QuoteDto ToDto(OpenQuote q) => new(q.Accepted, q.Errors, q.Symbol, q.Side.ToString(), q.Leverage,
        q.MarkPrice, q.FillPrice, q.Quantity, q.Notional, q.Margin, q.OpenFee, q.LiquidationPrice, q.LossAtStop,
        q.ProfitAtTakeProfit, q.AvailableBalance);
}

public sealed record OpenRequestDto(Guid CommandId, string Symbol, string Side, int Leverage, decimal Margin,
    decimal StopLoss, decimal? TakeProfit)
{
    public OpenPositionRequest? ToRequest() =>
        Enum.TryParse<PositionSide>(Side, ignoreCase: true, out var side) && Enum.IsDefined(side)
            ? new OpenPositionRequest(CommandId, (Symbol ?? "").ToUpperInvariant(), side, Leverage, Margin, StopLoss, TakeProfit)
            : null;
}

public sealed record CloseRequestDto(Guid CommandId);

public sealed record QuoteDto(bool Accepted, IReadOnlyList<string> Errors, string Symbol, string Side, int Leverage,
    decimal? MarkPrice, decimal? FillPrice, decimal? Quantity, decimal? Notional, decimal? Margin, decimal? OpenFee,
    decimal? LiquidationPrice, decimal? LossAtStop, decimal? ProfitAtTakeProfit, decimal AvailableBalance);

public sealed record OpenResponseDto(bool Accepted, bool Duplicate, IReadOnlyList<string> Errors, PositionDto? Position);

public sealed record PositionDto(long Id, string Environment, string Symbol, string Side, int Leverage,
    decimal Quantity, decimal EntryPrice, decimal Margin, decimal LiquidationPrice, decimal StopLoss,
    decimal? TakeProfit, decimal OpenFee, decimal CloseFee, string Status, string? CloseReason, decimal? ExitPrice,
    decimal? RealizedPnl, decimal? NetPnl, DateTime OpenedAtUtc, DateTime? ClosedAtUtc, decimal? MarkPrice,
    decimal? UnrealizedPnl, decimal? RoePercent);

public sealed record MarkPriceDto(string Symbol, decimal Price, DateTime TimeUtc);

public sealed record RulesDto(int MaxLeverage, decimal TakerFeeRate, decimal SlippageRate, IReadOnlyList<string> Symbols);

public sealed record AccountDto(string Environment, decimal WalletBalance, decimal UsedMargin, decimal UnrealizedPnl,
    decimal Equity, decimal AvailableBalance, IReadOnlyList<PositionDto> OpenPositions,
    IReadOnlyList<MarkPriceDto> MarkPrices, RulesDto Rules);
