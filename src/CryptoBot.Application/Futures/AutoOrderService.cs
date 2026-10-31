using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Indicators;
using CryptoBot.Domain.Futures;
using CryptoBot.Domain.Indicators;
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

    /// <summary>Quy tắc đã mở lệnh và đang chờ chỉ báo đảo chiều để tự đóng.</summary>
    Task<IReadOnlyList<AutoOrderRule>> GetWatchingCloseAsync(CancellationToken ct);
    Task<IReadOnlyList<AutoOrderRule>> GetRecentAsync(int limit, CancellationToken ct);
    Task<AutoOrderRule?> GetAsync(long id, CancellationToken ct);
    void Add(AutoOrderRule rule);
    Task SaveAsync(CancellationToken ct);
}

public sealed record CreateRuleRequest(
    string Symbol,
    PositionSide Side,
    TriggerCondition? Condition,
    decimal? TriggerPrice,
    decimal Margin,
    int Leverage,
    decimal StopLossPercent,
    decimal? TakeProfitPercent,
    RuleTriggerType TriggerType = RuleTriggerType.Price,
    string? IndicatorName = null,
    bool CloseOnReverse = false);

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
/// Quy tắc tự mở lệnh Paper theo mức giá hoặc theo tín hiệu chỉ báo tự viết; quy tắc theo chỉ báo có thể tự đóng
/// vị thế khi chỉ báo đảo chiều. Mọi lệnh vẫn đi qua <see cref="PaperFuturesService"/>
/// (cùng kiểm tra SL/giá thanh lý/số dư/min notional). Chỉ chạy Paper, không có đường tới tiền thật.
/// </summary>
public sealed class AutoOrderService(
    IAutoOrderStore store,
    IPaperFuturesStore paperStore,
    PaperFuturesService paper,
    MarkPriceCache marks,
    IndicatorService indicators,
    IndicatorRegistry indicatorRegistry,
    PaperFuturesOptions paperOptions,
    AutoOrderOptions options,
    IClock clock,
    ILogger<AutoOrderService> logger)
{
    public async Task<CreateRuleResult> CreateAsync(CreateRuleRequest request, CancellationToken ct)
    {
        var current = marks.GetFresh(request.Symbol);
        var errors = new List<string>();
        AutoOrderRule? rule = null;

        if (request.TriggerType == RuleTriggerType.Indicator)
        {
            if (indicatorRegistry.Find(request.IndicatorName) is not { } ind)
                errors.Add($"Không có chỉ báo '{request.IndicatorName}' trong cấu hình.");
            else if (!string.Equals(ind.Key.Symbol, request.Symbol, StringComparison.OrdinalIgnoreCase))
                errors.Add($"Chỉ báo '{ind.Name}' chạy trên {ind.Key.Symbol}, không phải {request.Symbol}.");
            if (current is null)
                errors.Add("Chưa có mark price mới; chưa thể tạo quy tắc.");
            else
                // Không có giá kích hoạt: kiểm tra khối lượng/SL theo giá hiện tại (ước tính).
                errors.AddRange(Validate(request, current.Price));
            if (errors.Count == 0)
                rule = AutoOrderRule.ForIndicator(request.Symbol, request.Side,
                    indicatorRegistry.Find(request.IndicatorName)!.Name,
                    request.Margin, request.Leverage, request.StopLossPercent, request.TakeProfitPercent, clock.UtcNow,
                        request.CloseOnReverse);
        }
        else if (request.TriggerType == RuleTriggerType.Price)
        {
            if (request.Condition is not { } cond || !Enum.IsDefined(cond)) errors.Add("Điều kiện không hợp lệ.");
            if (request.TriggerPrice is not > 0) errors.Add("Giá kích hoạt phải lớn hơn 0.");
            if (request.CloseOnReverse) errors.Add("Tự đóng khi đảo chiều chỉ dùng với quy tắc theo chỉ báo.");
            if (errors.Count == 0)
            {
                errors.AddRange(Validate(request, request.TriggerPrice!.Value));
                rule = new AutoOrderRule(request.Symbol, request.Side, request.Condition!.Value,
                    request.TriggerPrice!.Value, request.Margin,
                    request.Leverage, request.StopLossPercent, request.TakeProfitPercent, clock.UtcNow);
                if (current is null)
                    errors.Add("Chưa có mark price mới; chưa thể tạo quy tắc.");
                else if (rule.IsMet(current.Price))
                    errors.Add($"Giá hiện tại {current.Price} đã thỏa điều kiện; quy tắc sẽ kích hoạt ngay. " +
                               "Đặt mức giá xa hơn nếu bạn muốn chờ.");
            }
        }
        else
        {
            errors.Add("Kiểu kích hoạt không hợp lệ.");
        }

        if (errors.Count == 0 && (await store.GetActiveAsync(ct)).Count >= options.MaxActiveRules)
            errors.Add($"Đã có tối đa {options.MaxActiveRules} quy tắc đang chạy. Hủy bớt trước khi tạo mới.");
        if (errors.Count > 0 || rule is null)
            return new CreateRuleResult(null, errors);

        store.Add(rule);
        await store.SaveAsync(ct);
        if (rule.TriggerType == RuleTriggerType.Indicator)
            logger.LogInformation("Auto rule #{Id}: khi chỉ báo {Indicator} báo {Side} -> mở {Side} {Lev}x margin {Margin}, " +
                                  "tự đóng khi đảo chiều: {CloseOnReverse}",
                rule.Id, rule.IndicatorName, rule.Side, rule.Side, rule.Leverage, rule.Margin, rule.CloseOnReverse);
        else
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

    /// <summary>
    /// Kiểm tra các quy tắc theo mark price và chỉ báo mới: mở lệnh cho quy tắc đang chờ, rồi tự đóng vị thế
    /// của quy tắc có chỉ báo vừa đảo chiều. Trả về số lệnh đã mở + đã đóng.
    /// </summary>
    public async Task<int> EvaluateAsync(CancellationToken ct)
    {
        var readings = new Dictionary<string, IndicatorReading?>(StringComparer.OrdinalIgnoreCase);
        var opened = await EvaluateOpensAsync(readings, ct);
        return opened + await EvaluateClosesAsync(readings, ct);
    }

    /// <summary>Mở lệnh cho các quy tắc đang chờ khi điều kiện xảy ra. Trả về số quy tắc đã kích hoạt.</summary>
    private async Task<int> EvaluateOpensAsync(Dictionary<string, IndicatorReading?> readings, CancellationToken ct)
    {
        var active = await store.GetActiveAsync(ct);
        if (active.Count == 0)
            return 0;

        DailyRiskState? risk = null;
        var fired = 0;
        var changed = false;

        foreach (var rule in active)
        {
            if (marks.GetFresh(rule.Symbol) is not { } mark)
                continue;

            string? detail = null;
            if (rule.TriggerType == RuleTriggerType.Indicator)
            {
                var reading = await ReadIndicatorAsync(rule.IndicatorName!, readings, ct);
                if (!SignalAfter(reading, rule.CreatedAtUtc, rule.Side))
                    continue;
                detail = Describe(reading!);
            }
            else if (!rule.IsMet(mark.Price))
            {
                continue;
            }

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
                rule.MarkTriggered(mark.Price, position.Id, clock.UtcNow, detail);
                fired++;
                logger.LogWarning("Auto rule #{Id} KÍCH HOẠT: {Symbol} mark {Mark} -> {Side} {Qty} @ {Entry}",
                    rule.Id, rule.Symbol, mark.Price, position.Side, position.Quantity, position.EntryPrice);
            }
            else
            {
                rule.MarkRejected(mark.Price, string.Join(" ", result.Quote.Errors), clock.UtcNow, detail);
                logger.LogWarning("Auto rule #{Id} bị từ chối: {Reason}", rule.Id, rule.Note);
            }

            changed = true;
        }

        if (changed)
            await store.SaveAsync(ct);
        return fired;
    }

    /// <summary>
    /// Tự đóng vị thế của các quy tắc bật <see cref="AutoOrderRule.CloseOnReverse"/> khi chỉ báo phát tín hiệu
    /// ngược hướng trên nến đóng sau lúc mở lệnh. Không bị chặn bởi giới hạn lỗ ngày (đóng lệnh chỉ giảm rủi ro).
    /// Trả về số vị thế đã đóng.
    /// </summary>
    private async Task<int> EvaluateClosesAsync(Dictionary<string, IndicatorReading?> readings, CancellationToken ct)
    {
        var watching = await store.GetWatchingCloseAsync(ct);
        if (watching.Count == 0)
            return 0;

        var closed = 0;
        var changed = false;
        foreach (var rule in watching)
        {
            var position = await paperStore.GetAsync(rule.PositionId!.Value, ct);
            // Đã đóng bằng chính lệnh đóng của quy tắc (ví dụ lần trước lưu vị thế xong nhưng chưa lưu quy tắc).
            if (position is { Status: not PositionStatus.Open } && position.CloseCommandId == rule.CloseCommandId)
            {
                rule.EndCloseWatch("Đã tự đóng khi chỉ báo đảo chiều.", clock.UtcNow);
                changed = true;
                continue;
            }

            // SL/TP/thanh lý/đóng tay đến trước: không còn gì để đóng.
            if (position is not { Status: PositionStatus.Open })
            {
                rule.EndCloseWatch($"Vị thế đã kết thúc trước ({position?.CloseReason?.ToString() ?? "không tìm thấy"}).",
                    clock.UtcNow);
                changed = true;
                continue;
            }

            var reading = await ReadIndicatorAsync(rule.IndicatorName!, readings, ct);
            var opposite = rule.Side == PositionSide.Long ? PositionSide.Short : PositionSide.Long;
            if (!SignalAfter(reading, rule.TriggeredAtUtc!.Value, opposite))
                continue;

            var result = await paper.CloseAsync(position.Id, rule.CloseCommandId!.Value, CloseReason.Signal, ct);
            if (!result.Ok)
            {
                // Ví dụ chưa có mark price mới: giữ theo dõi, thử lại ở lần kiểm tra sau.
                logger.LogWarning("Auto rule #{Id} chưa tự đóng được vị thế {PositionId}: {Error}",
                    rule.Id, position.Id, result.Error);
                continue;
            }

            rule.EndCloseWatch(Describe(reading!), clock.UtcNow);
            changed = true;
            closed++;
            logger.LogWarning("Auto rule #{Id} TỰ ĐÓNG {Side} {Symbol} vị thế {PositionId} @ {Exit}: chỉ báo đảo chiều",
                rule.Id, position.Side, position.Symbol, position.Id, position.ExitPrice);
        }

        if (changed)
            await store.SaveAsync(ct);
        return closed;
    }

    /// <summary>Đọc kết quả mới nhất của chỉ báo, mỗi chỉ báo chỉ tính một lần trong một lượt kiểm tra.</summary>
    private async Task<IndicatorReading?> ReadIndicatorAsync(string name, Dictionary<string, IndicatorReading?> readings,
        CancellationToken ct)
    {
        if (!readings.TryGetValue(name, out var reading))
            readings[name] = reading = await indicators.GetLatestAsync(name, ct);
        return reading;
    }

    /// <summary>
    /// Tín hiệu chỉ được dùng khi: nến đã đóng SAU <paramref name="afterUtc"/> (lúc tạo quy tắc khi mở, lúc mở lệnh
    /// khi đóng; không ăn tín hiệu cũ), nến là nến mới nhất đáng lẽ phải có (dữ liệu không bị trễ quá một khung + biên),
    /// và tín hiệu cùng hướng <paramref name="side"/>.
    /// </summary>
    private bool SignalAfter(IndicatorReading? reading, DateTime afterUtc, PositionSide side)
    {
        if (reading is null || reading.Result.Signal == IndicatorSignal.None)
            return false;
        var candle = reading.Candle;
        if (candle.CloseTimeUtc <= afterUtc)
            return false;
        if (clock.UtcNow - candle.CloseTimeUtc > reading.Indicator.Key.Interval.Duration + IndicatorStaleGrace)
            return false;
        return reading.Result.Signal == (side == PositionSide.Long ? IndicatorSignal.Long : IndicatorSignal.Short);
    }

    /// <summary>Thời gian chờ đồng bộ nến sau khi nến đóng trước khi coi dữ liệu là trễ.</summary>
    public static readonly TimeSpan IndicatorStaleGrace = TimeSpan.FromMinutes(2);

    private static string Describe(IndicatorReading reading)
    {
        var values = string.Join(", ", reading.Result.Values.Where(pair => pair.Value is not null)
            .Select(pair => $"{pair.Key}={pair.Value:0.####}"));
        var text = $"{reading.Indicator.Name} nến {reading.Candle.OpenTimeUtc:yyyy-MM-dd HH:mm} UTC: {reading.Result.Reason}";
        if (values.Length > 0)
            text += $" [{values}]";
        return text.Length <= 500 ? text : text[..500];
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

        var dayStartEquity = ledger.Where(entry => entry.TimeUtc < dayStart).Sum(entry => entry.Amount);
        if (dayStartEquity <= 0)
            dayStartEquity = paperOptions.StartingBalance;

        var todayPnl = ledger
            .Where(entry => entry.TimeUtc >= dayStart && entry.Type != LedgerEntryType.Deposit)
            .Sum(entry => entry.Amount);
        var limit = dayStartEquity * options.DailyLossLimitPercent / 100m;
        return new DailyRiskState(dayStart, dayStartEquity, todayPnl, limit, todayPnl <= -limit);
    }

    private OpenPositionRequest BuildRequest(AutoOrderRule rule, decimal mark)
    {
        var rules = paperOptions.FindRules(rule.Symbol);
        decimal Round(decimal price) => rules is null ? price : FuturesMath.RoundToTick(price, rules.TickSize);

        var dir = rule.Side == PositionSide.Long ? 1m : -1m;
        var stop = Round(mark * (1 - dir * rule.StopLossPercent / 100m));
        decimal? take = rule.TakeProfitPercent is { } tp ? Round(mark * (1 + dir * tp / 100m)) : null;
        return new OpenPositionRequest(rule.CommandId, rule.Symbol, rule.Side, rule.Leverage, rule.Margin, stop, take);
    }

    /// <summary>Kiểm tra margin/đòn bẩy/SL. <paramref name="refPrice"/>: giá kích hoạt, hoặc giá hiện tại với quy tắc chỉ báo.</summary>
    private List<string> Validate(CreateRuleRequest request, decimal refPrice)
    {
        var errors = new List<string>();
        var rules = paperOptions.FindRules(request.Symbol);
        if (rules is null)
        {
            errors.Add($"Symbol {request.Symbol} chưa được cấu hình cho paper futures.");
            return errors;
        }

        if (!Enum.IsDefined(request.Side)) errors.Add("Hướng lệnh không hợp lệ.");
        if (request.Margin <= 0) errors.Add("Margin phải lớn hơn 0.");
        if (request.Leverage < 1 || request.Leverage > paperOptions.MaxLeverage)
            errors.Add($"Đòn bẩy phải từ 1 đến {paperOptions.MaxLeverage}x.");
        if (request.StopLossPercent <= 0 || request.StopLossPercent >= 50)
            errors.Add("Stop loss (%) phải lớn hơn 0 và nhỏ hơn 50.");
        if (request.TakeProfitPercent is <= 0)
            errors.Add("Take profit (%) phải lớn hơn 0.");
        if (errors.Count > 0)
            return errors;

        var qtyAtTrigger = FuturesMath.RoundDown(request.Margin * request.Leverage / refPrice, rules.QuantityStep);
        if (qtyAtTrigger <= 0)
            errors.Add($"Ở giá {refPrice:0.##}, margin × đòn bẩy = {request.Margin * request.Leverage:0.##} USDT không đủ mua " +
                       $"một bước khối lượng ({rules.QuantityStep}). Tăng margin hoặc đòn bẩy.");
        else if (request.Margin * request.Leverage < rules.MinNotional)
            errors.Add($"Margin × đòn bẩy = {request.Margin * request.Leverage:0.##} USDT nhỏ hơn giá trị vị thế tối thiểu {rules.MinNotional} USDT.");

        // Stop loss phải nằm trước giá thanh lý (ước tính tại giá kích hoạt).
        var liq = FuturesMath.LiquidationPrice(request.Side, refPrice, request.Leverage, rules.MaintenanceMarginRate);
        var dir = request.Side == PositionSide.Long ? 1m : -1m;
        var stop = refPrice * (1 - dir * request.StopLossPercent / 100m);
        if (request.Side == PositionSide.Long ? stop <= liq : stop >= liq)
            errors.Add($"Stop loss {request.StopLossPercent}% nằm sau giá thanh lý ước tính {liq:0.##} ở đòn bẩy {request.Leverage}x. " +
                       "Giảm đòn bẩy hoặc giảm % stop loss.");
        return errors;
    }
}
