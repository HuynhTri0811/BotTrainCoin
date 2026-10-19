using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.Futures;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Application.Futures;

public sealed class AutoOrderOptions
{
    public decimal DefaultMargin { get; set; } = 50m;
    public int DefaultLeverage { get; set; } = 2;
    public decimal DefaultStopLossPercent { get; set; } = 1m;
    public decimal DefaultTakeProfitPercent { get; set; } = 2m;

    /// <summary>Chặn lệnh tự động mới khi lỗ trong ngày UTC vượt mức này so với vốn đầu ngày (%).</summary>
    public decimal DailyLossLimitPercent { get; set; } = 5m;

    public int MaxActiveRules { get; set; } = 10;
}

public interface IAutoOrderStore
{
    Task<IReadOnlyList<AutoOrderRule>> GetActiveAsync(CancellationToken ct);
    Task<IReadOnlyList<AutoOrderRule>> GetRecentAsync(int limit, CancellationToken ct);
    Task<AutoOrderRule?> GetAsync(long id, CancellationToken ct);
    void Add(AutoOrderRule rule);
    Task SaveAsync(CancellationToken ct);
}

public sealed record CreateRuleRequest(
    string Symbol,
    PositionSide Side,
    TriggerCondition Condition,
    decimal TriggerPrice,
    decimal Margin,
    int Leverage,
    decimal StopLossPercent,
    decimal? TakeProfitPercent);

public sealed record CreateRuleResult(AutoOrderRule? Rule, IReadOnlyList<string> Errors);

public sealed record DailyRiskState(
    DateTime DayStartUtc,
    decimal DayStartEquity,
    decimal TodayRealizedPnl,
    decimal LossLimit,
    bool Blocked);

public sealed record AutoOrderSummary(
    IReadOnlyList<AutoOrderRule> Rules,
    AutoOrderOptions Defaults,
    DailyRiskState Risk);

/// <summary>
/// Quy tắc tự mở lệnh Paper theo mức giá. Mọi lệnh vẫn đi qua <see cref="PaperFuturesService"/>
/// (cùng kiểm tra SL/giá thanh lý/số dư/min notional). Chỉ chạy Paper, không có đường tới tiền thật.
/// </summary>
public sealed class AutoOrderService(
    IAutoOrderStore store,
    IPaperFuturesStore paperStore,
    PaperFuturesService paper,
    MarkPriceCache marks,
    PaperFuturesOptions paperOptions,
    AutoOrderOptions options,
    IClock clock,
    ILogger<AutoOrderService> logger)
{
    public async Task<CreateRuleResult> CreateAsync(CreateRuleRequest r, CancellationToken ct)
    {
        var errors = Validate(r);
        var current = marks.GetFresh(r.Symbol);
        if (current is null)
            errors.Add("Chưa có mark price mới; chưa thể tạo quy tắc.");
        else if (new AutoOrderRule(r.Symbol, r.Side, r.Condition, r.TriggerPrice, r.Margin, r.Leverage,
                     r.StopLossPercent, r.TakeProfitPercent, clock.UtcNow).IsMet(current.Price))
            errors.Add($"Giá hiện tại {current.Price} đã thỏa điều kiện; quy tắc sẽ kích hoạt ngay. " +
                       "Đặt mức giá xa hơn nếu bạn muốn chờ.");

        if (errors.Count == 0 && (await store.GetActiveAsync(ct)).Count >= options.MaxActiveRules)
            errors.Add($"Đã có tối đa {options.MaxActiveRules} quy tắc đang chạy. Hủy bớt trước khi tạo mới.");
        if (errors.Count > 0)
            return new CreateRuleResult(null, errors);

        var rule = new AutoOrderRule(r.Symbol, r.Side, r.Condition, r.TriggerPrice, r.Margin, r.Leverage,
            r.StopLossPercent, r.TakeProfitPercent, clock.UtcNow);
        store.Add(rule);
        await store.SaveAsync(ct);
        logger.LogInformation("Auto rule #{Id}: khi {Symbol} {Cond} {Price} -> mở {Side} {Lev}x margin {Margin}",
            rule.Id, rule.Symbol, rule.Condition, rule.TriggerPrice, rule.Side, rule.Leverage, rule.Margin);
        return new CreateRuleResult(rule, []);
    }

    public async Task<bool> CancelAsync(long id, CancellationToken ct)
    {
        var rule = await store.GetAsync(id, ct);
        if (rule is null || rule.Status != AutoRuleStatus.Active)
            return false;
        rule.Cancel();
        await store.SaveAsync(ct);
        return true;
    }

    /// <summary>Kiểm tra các quy tắc đang chạy theo mark price mới. Trả về số quy tắc đã kích hoạt.</summary>
    public async Task<int> EvaluateAsync(CancellationToken ct)
    {
        var active = await store.GetActiveAsync(ct);
        if (active.Count == 0)
            return 0;

        DailyRiskState? risk = null;
        var fired = 0;
        var changed = false;

        foreach (var rule in active)
        {
            if (marks.GetFresh(rule.Symbol) is not { } mark || !rule.IsMet(mark.Price))
                continue;

            risk ??= await GetRiskAsync(ct);
            if (risk.Blocked)
            {
                changed |= rule.SetBlockedNote(
                    $"Bị chặn: lỗ hôm nay {risk.TodayRealizedPnl:0.##} USDT đã chạm giới hạn −{risk.LossLimit:0.##} USDT.");
                continue;
            }

            var request = BuildRequest(rule, mark.Price);
            var result = await paper.OpenAsync(request, ct);
            if (result.Position is { } position)
            {
                rule.MarkTriggered(mark.Price, position.Id, clock.UtcNow);
                fired++;
                logger.LogWarning("Auto rule #{Id} KÍCH HOẠT: {Symbol} mark {Mark} -> {Side} {Qty} @ {Entry}",
                    rule.Id, rule.Symbol, mark.Price, position.Side, position.Quantity, position.EntryPrice);
            }
            else
            {
                rule.MarkRejected(mark.Price, string.Join(" ", result.Quote.Errors), clock.UtcNow);
                logger.LogWarning("Auto rule #{Id} bị từ chối: {Reason}", rule.Id, rule.Note);
            }

            changed = true;
        }

        if (changed)
            await store.SaveAsync(ct);
        return fired;
    }

    public async Task<AutoOrderSummary> GetSummaryAsync(CancellationToken ct) =>
        new(await store.GetRecentAsync(50, ct), options, await GetRiskAsync(ct));

    /// <summary>
    /// Lỗ thực hiện trong ngày UTC (PnL đã chốt + phí) so với vốn đầu ngày. Vốn đầu ngày lấy từ sổ vốn
    /// trước 00:00 UTC; ngày đầu tiên dùng vốn khởi tạo.
    /// </summary>
    public async Task<DailyRiskState> GetRiskAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var ledger = await paperStore.GetLedgerAsync(ct);

        var dayStartEquity = ledger.Where(e => e.TimeUtc < dayStart).Sum(e => e.Amount);
        if (dayStartEquity <= 0)
            dayStartEquity = paperOptions.StartingBalance;

        var todayPnl = ledger
            .Where(e => e.TimeUtc >= dayStart && e.Type != LedgerEntryType.Deposit)
            .Sum(e => e.Amount);
        var limit = dayStartEquity * options.DailyLossLimitPercent / 100m;
        return new DailyRiskState(dayStart, dayStartEquity, todayPnl, limit, todayPnl <= -limit);
    }

    private OpenPositionRequest BuildRequest(AutoOrderRule rule, decimal mark)
    {
        var rules = paperOptions.FindRules(rule.Symbol);
        decimal Round(decimal p) => rules is null ? p : FuturesMath.RoundToTick(p, rules.TickSize);

        var dir = rule.Side == PositionSide.Long ? 1m : -1m;
        var stop = Round(mark * (1 - dir * rule.StopLossPercent / 100m));
        decimal? take = rule.TakeProfitPercent is { } tp ? Round(mark * (1 + dir * tp / 100m)) : null;
        return new OpenPositionRequest(rule.CommandId, rule.Symbol, rule.Side, rule.Leverage, rule.Margin, stop, take);
    }

    private List<string> Validate(CreateRuleRequest r)
    {
        var errors = new List<string>();
        var rules = paperOptions.FindRules(r.Symbol);
        if (rules is null)
        {
            errors.Add($"Symbol {r.Symbol} chưa được cấu hình cho paper futures.");
            return errors;
        }

        if (!Enum.IsDefined(r.Side)) errors.Add("Hướng lệnh không hợp lệ.");
        if (!Enum.IsDefined(r.Condition)) errors.Add("Điều kiện không hợp lệ.");
        if (r.TriggerPrice <= 0) errors.Add("Giá kích hoạt phải lớn hơn 0.");
        if (r.Margin <= 0) errors.Add("Margin phải lớn hơn 0.");
        if (r.Leverage < 1 || r.Leverage > paperOptions.MaxLeverage)
            errors.Add($"Đòn bẩy phải từ 1 đến {paperOptions.MaxLeverage}x.");
        if (r.StopLossPercent <= 0 || r.StopLossPercent >= 50)
            errors.Add("Stop loss (%) phải lớn hơn 0 và nhỏ hơn 50.");
        if (r.TakeProfitPercent is <= 0)
            errors.Add("Take profit (%) phải lớn hơn 0.");
        if (errors.Count > 0)
            return errors;

        var qtyAtTrigger = FuturesMath.RoundDown(r.Margin * r.Leverage / r.TriggerPrice, rules.QuantityStep);
        if (qtyAtTrigger <= 0)
            errors.Add($"Ở giá {r.TriggerPrice:0.##}, margin × đòn bẩy = {r.Margin * r.Leverage:0.##} USDT không đủ mua " +
                       $"một bước khối lượng ({rules.QuantityStep}). Tăng margin hoặc đòn bẩy.");
        else if (r.Margin * r.Leverage < rules.MinNotional)
            errors.Add($"Margin × đòn bẩy = {r.Margin * r.Leverage:0.##} USDT nhỏ hơn giá trị vị thế tối thiểu {rules.MinNotional} USDT.");

        // Stop loss phải nằm trước giá thanh lý (ước tính tại giá kích hoạt).
        var liq = FuturesMath.LiquidationPrice(r.Side, r.TriggerPrice, r.Leverage, rules.MaintenanceMarginRate);
        var dir = r.Side == PositionSide.Long ? 1m : -1m;
        var stop = r.TriggerPrice * (1 - dir * r.StopLossPercent / 100m);
        if (r.Side == PositionSide.Long ? stop <= liq : stop >= liq)
            errors.Add($"Stop loss {r.StopLossPercent}% nằm sau giá thanh lý ước tính {liq:0.##} ở đòn bẩy {r.Leverage}x. " +
                       "Giảm đòn bẩy hoặc giảm % stop loss.");
        return errors;
    }
}
