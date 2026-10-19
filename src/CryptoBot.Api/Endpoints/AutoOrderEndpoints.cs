using CryptoBot.Application.Futures;
using CryptoBot.Domain.Futures;

namespace CryptoBot.Api.Endpoints;

/// <summary>Quy tắc tự mở lệnh Paper theo mức giá hoặc theo chỉ báo. Môi trường Paper nằm trong đường dẫn.</summary>
public static class AutoOrderEndpoints
{
    public static void MapAutoOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/paper/futures/auto-rules");

        api.MapGet("", async (AutoOrderService svc, CancellationToken ct) =>
        {
            var s = await svc.GetSummaryAsync(ct);
            return new AutoSummaryDto(s.Rules.Select(ToDto).ToList(),
                new AutoDefaultsDto(s.Defaults.DefaultMargin, s.Defaults.DefaultLeverage,
                    s.Defaults.DefaultStopLossPercent, s.Defaults.DefaultTakeProfitPercent,
                    s.Defaults.DailyLossLimitPercent, s.Defaults.MaxActiveRules),
                new DailyRiskDto(s.Risk.DayStartUtc, s.Risk.DayStartEquity, s.Risk.TodayRealizedPnl,
                    s.Risk.LossLimit, s.Risk.Blocked));
        });

        api.MapPost("", async (CreateRuleDto body, AutoOrderService svc, CancellationToken ct) =>
        {
            var type = RuleTriggerType.Price;
            TriggerCondition? cond = null;
            if (!Enum.TryParse<PositionSide>(body.Side, true, out var side) ||
                (body.TriggerType is not null && !Enum.TryParse(body.TriggerType, true, out type)))
                return Results.BadRequest(new { errors = new[] { "Side hoặc TriggerType không hợp lệ." } });
            if (type == RuleTriggerType.Price)
            {
                if (!Enum.TryParse<TriggerCondition>(body.Condition, true, out var c))
                    return Results.BadRequest(new { errors = new[] { "Condition không hợp lệ." } });
                cond = c;
            }

            var result = await svc.CreateAsync(new CreateRuleRequest((body.Symbol ?? "").ToUpperInvariant(), side,
                cond, body.TriggerPrice, body.Margin, body.Leverage, body.StopLossPercent, body.TakeProfitPercent,
                type, body.IndicatorName), ct);
            return result.Rule is { } rule
                ? Results.Ok(ToDto(rule))
                : Results.UnprocessableEntity(new { errors = result.Errors });
        });

        api.MapPost("/{id:long}/cancel", async (long id, AutoOrderService svc, CancellationToken ct) =>
            await svc.CancelAsync(id, ct)
                ? Results.Ok(new { cancelled = true })
                : Results.UnprocessableEntity(new { error = "Không tìm thấy quy tắc đang chạy." }));
    }

    private static AutoRuleDto ToDto(AutoOrderRule r) => new(r.Id, r.Environment, r.Symbol, r.Side.ToString(),
        r.TriggerType.ToString(), r.Condition?.ToString(), r.TriggerPrice, r.IndicatorName, r.Margin, r.Leverage,
        r.StopLossPercent, r.TakeProfitPercent, r.Status.ToString(), r.CreatedAtUtc, r.TriggeredAtUtc,
        r.TriggeredMarkPrice, r.PositionId, r.Note, r.TriggerDetail);
}

/// <summary>TriggerType: "Price" (mặc định, cần Condition + TriggerPrice) hoặc "Indicator" (cần IndicatorName).</summary>
public sealed record CreateRuleDto(string Symbol, string Side, string? Condition, decimal? TriggerPrice, decimal Margin,
    int Leverage, decimal StopLossPercent, decimal? TakeProfitPercent, string? TriggerType = null,
    string? IndicatorName = null);

public sealed record AutoRuleDto(long Id, string Environment, string Symbol, string Side, string TriggerType,
    string? Condition, decimal? TriggerPrice, string? IndicatorName, decimal Margin, int Leverage,
    decimal StopLossPercent, decimal? TakeProfitPercent, string Status, DateTime CreatedAtUtc,
    DateTime? TriggeredAtUtc, decimal? TriggeredMarkPrice, long? PositionId, string? Note, string? TriggerDetail);

public sealed record AutoDefaultsDto(decimal Margin, int Leverage, decimal StopLossPercent, decimal TakeProfitPercent,
    decimal DailyLossLimitPercent, int MaxActiveRules);

public sealed record DailyRiskDto(DateTime DayStartUtc, decimal DayStartEquity, decimal TodayRealizedPnl,
    decimal LossLimit, bool Blocked);

public sealed record AutoSummaryDto(IReadOnlyList<AutoRuleDto> Rules, AutoDefaultsDto Defaults, DailyRiskDto Risk);
