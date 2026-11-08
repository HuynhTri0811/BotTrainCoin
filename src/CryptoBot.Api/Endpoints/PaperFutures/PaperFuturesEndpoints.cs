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
            var account = await svc.GetAccountAsync(ct);
            return new AccountDto("Paper", account.WalletBalance, account.UsedMargin, account.UnrealizedPnl,
                account.Equity, account.AvailableBalance,
                account.OpenPositions.Select(view => ToDto(view.Position, view.MarkPrice, view.UnrealizedPnl,
                    view.RoePercent)).ToList(),
                account.MarkPrices.Select(markPrice => new MarkPriceDto(markPrice.Symbol, markPrice.Price,
                    markPrice.TimeUtc)).ToList(),
                new RulesDto(options.MaxLeverage, options.TakerFeeRate, options.SlippageRate,
                    options.Symbols.Select(symbolOptions => symbolOptions.Symbol).ToList()));
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
            return result.Position is { } position
                ? Results.Ok(new OpenResponseDto(true, result.Duplicate, [], ToDto(position, null, null, null)))
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

        // Mới → cũ; lọc theo symbol, hướng, lý do đóng và khoảng giờ đóng [fromUtc, toUtc).
        api.MapGet("/positions/history", async (string? symbol, string? side, string? closeReason,
            DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int? limit, IPaperFuturesStore store, CancellationToken ct) =>
        {
            if (!QueryEnum.TryParse<PositionSide>(side, out var pickedSide))
                return Results.BadRequest(new { error = QueryEnum.Error<PositionSide>(side!) });
            if (!QueryEnum.TryParse<CloseReason>(closeReason, out var pickedReason))
                return Results.BadRequest(new { error = QueryEnum.Error<CloseReason>(closeReason!) });

            var filter = new ClosedPositionFilter(
                string.IsNullOrWhiteSpace(symbol) ? null : symbol.Trim().ToUpperInvariant(), pickedSide, pickedReason,
                fromUtc?.UtcDateTime, toUtc?.UtcDateTime);
            var closed = await store.GetClosedAsync(filter, Math.Clamp(limit ?? 50, 1, 500), ct);
            return Results.Ok(closed.Select(position => ToDto(position, null, null, null)));
        });
    }

    private static PositionDto ToDto(FuturesPosition position, decimal? mark, decimal? upnl, decimal? roe) => new(
        position.Id, position.Environment, position.Symbol, position.Side.ToString(), position.Leverage,
            position.Quantity, position.EntryPrice, position.Margin,
        position.LiquidationPrice, position.StopLoss, position.TakeProfit, position.OpenFee, position.CloseFee,
            position.Status.ToString(),
        position.CloseReason?.ToString(), position.ExitPrice, position.RealizedPnl,
            position.Status == PositionStatus.Open ? null : position.NetPnl,
        position.OpenedAtUtc, position.ClosedAtUtc, mark, upnl, roe);

    private static QuoteDto ToDto(OpenQuote quote) => new(quote.Accepted, quote.Errors, quote.Symbol,
        quote.Side.ToString(), quote.Leverage,
        quote.MarkPrice, quote.FillPrice, quote.Quantity, quote.Notional, quote.Margin, quote.OpenFee,
            quote.LiquidationPrice, quote.LossAtStop,
        quote.ProfitAtTakeProfit, quote.AvailableBalance);
}
