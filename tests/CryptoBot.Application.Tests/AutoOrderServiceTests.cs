using CryptoBot.Application.Futures;
using CryptoBot.Domain.Futures;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.Application.Tests;

internal sealed class InMemoryAutoOrderStore : IAutoOrderStore
{
    public readonly List<AutoOrderRule> Rules = [];
    private long _nextId = 1;

    public Task<IReadOnlyList<AutoOrderRule>> GetActiveAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AutoOrderRule>>(Rules.Where(r => r.Status == AutoRuleStatus.Active).ToList());

    public Task<IReadOnlyList<AutoOrderRule>> GetRecentAsync(int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AutoOrderRule>>(Rules.AsEnumerable().Reverse().Take(limit).ToList());

    public Task<AutoOrderRule?> GetAsync(long id, CancellationToken ct) =>
        Task.FromResult(Rules.FirstOrDefault(r => r.Id == id));

    public void Add(AutoOrderRule rule)
    {
        typeof(AutoOrderRule).GetProperty(nameof(AutoOrderRule.Id))!.SetValue(rule, _nextId++);
        Rules.Add(rule);
    }

    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
}

public class AutoOrderServiceTests
{
    private static readonly DateTime T0 = new(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeClock _clock = new(T0);
    private readonly InMemoryPaperFuturesStore _paperStore = new();
    private readonly InMemoryAutoOrderStore _ruleStore = new();
    private readonly PaperFuturesOptions _paperOptions = new()
    {
        StartingBalance = 1000m,
        TakerFeeRate = 0.0005m,
        SlippageRate = 0m,
        MaxLeverage = 10,
        Symbols = [new FuturesSymbolOptions { Symbol = "BTCUSDT", QuantityStep = 0.001m, TickSize = 0.1m, MinNotional = 50m, MaintenanceMarginRate = 0.004m }],
    };
    private readonly AutoOrderOptions _options = new();
    private readonly MarkPriceCache _marks;
    private readonly PaperFuturesService _paper;
    private readonly AutoOrderService _svc;

    public AutoOrderServiceTests()
    {
        _marks = new MarkPriceCache(_clock, _paperOptions);
        _paper = new PaperFuturesService(_paperStore, _marks, new PaperFuturesLock(), _paperOptions, _clock,
            NullLogger<PaperFuturesService>.Instance);
        _svc = new AutoOrderService(_ruleStore, _paperStore, _paper, _marks, _paperOptions, _options, _clock,
            NullLogger<AutoOrderService>.Instance);
        SetMark(100_000m);
        _paper.EnsureFundedAsync(default).GetAwaiter().GetResult();
    }

    private void SetMark(decimal price) => _marks.Set(new MarkPrice("BTCUSDT", price, _clock.UtcNow));

    private static CreateRuleRequest BuyDip(decimal trigger = 98_000m, decimal margin = 50m, int leverage = 2,
        decimal sl = 1m, decimal? tp = 2m, PositionSide side = PositionSide.Long,
        TriggerCondition cond = TriggerCondition.AtOrBelow) =>
        new("BTCUSDT", side, cond, trigger, margin, leverage, sl, tp);

    private async Task<AutoOrderRule> CreateRule(CreateRuleRequest? req = null)
    {
        var result = await _svc.CreateAsync(req ?? BuyDip(), default);
        return Assert.IsType<AutoOrderRule>(result.Rule);
    }

    [Fact]
    public async Task Does_not_fire_while_price_is_above_trigger()
    {
        await CreateRule();
        SetMark(99_000m);

        Assert.Equal(0, await _svc.EvaluateAsync(default));

        Assert.Empty(_paperStore.Positions);
        Assert.Equal(AutoRuleStatus.Active, _ruleStore.Rules.Single().Status);
    }

    [Fact]
    public async Task Fires_when_mark_reaches_trigger_and_opens_long_with_sl_tp_from_mark()
    {
        var rule = await CreateRule();
        SetMark(97_900m);

        Assert.Equal(1, await _svc.EvaluateAsync(default));

        var p = Assert.Single(_paperStore.Positions);
        Assert.Equal(PositionSide.Long, p.Side);
        Assert.Equal(2, p.Leverage);
        Assert.Equal(96_921m, p.StopLoss);        // 97900 * 0.99
        Assert.Equal(99_858m, p.TakeProfit);      // 97900 * 1.02
        Assert.Equal(AutoRuleStatus.Triggered, rule.Status);
        Assert.Equal(p.Id, rule.PositionId);
        Assert.Equal(97_900m, rule.TriggeredMarkPrice);
    }

    [Fact]
    public async Task Short_rule_above_trigger_places_stop_above_and_take_profit_below()
    {
        await CreateRule(BuyDip(trigger: 102_000m, margin: 100m, side: PositionSide.Short, cond: TriggerCondition.AtOrAbove));
        SetMark(102_500m);

        await _svc.EvaluateAsync(default);

        var p = Assert.Single(_paperStore.Positions);
        Assert.Equal(PositionSide.Short, p.Side);
        Assert.Equal(103_525m, p.StopLoss);       // 102500 * 1.01
        Assert.Equal(100_450m, p.TakeProfit);     // 102500 * 0.98
    }

    [Fact]
    public async Task Rule_is_one_shot_and_does_not_reopen_after_position_closes()
    {
        await CreateRule();
        SetMark(97_900m);
        await _svc.EvaluateAsync(default);
        await _paper.CloseAsync(_paperStore.Positions.Single().Id, Guid.NewGuid(), default);

        SetMark(97_000m);
        Assert.Equal(0, await _svc.EvaluateAsync(default));

        Assert.Single(_paperStore.Positions);
    }

    [Fact]
    public async Task Retry_after_crash_between_open_and_save_does_not_open_twice()
    {
        var rule = await CreateRule();
        SetMark(97_900m);
        // Mô phỏng: lệnh đã mở bằng đúng commandId của quy tắc nhưng trạng thái quy tắc chưa kịp lưu.
        await _paper.OpenAsync(new OpenPositionRequest(rule.CommandId, "BTCUSDT", PositionSide.Long, 2, 50m,
            96_921m, 99_858m), default);

        await _svc.EvaluateAsync(default);

        Assert.Single(_paperStore.Positions);
        Assert.Equal(AutoRuleStatus.Triggered, rule.Status);
    }

    [Fact]
    public async Task Rejected_by_paper_engine_marks_rule_rejected_with_reason()
    {
        await CreateRule(BuyDip(margin: 5_000m));
        SetMark(97_900m);

        await _svc.EvaluateAsync(default);

        var rule = _ruleStore.Rules.Single();
        Assert.Equal(AutoRuleStatus.Rejected, rule.Status);
        Assert.Contains("Không đủ số dư", rule.Note);
        Assert.Empty(_paperStore.Positions);
    }

    [Fact]
    public async Task Existing_position_on_symbol_rejects_the_auto_order()
    {
        await _paper.OpenAsync(new OpenPositionRequest(Guid.NewGuid(), "BTCUSDT", PositionSide.Long, 2, 50m,
            95_000m, null), default);
        await CreateRule();
        SetMark(97_900m);

        await _svc.EvaluateAsync(default);

        Assert.Equal(AutoRuleStatus.Rejected, _ruleStore.Rules.Single().Status);
        Assert.Single(_paperStore.Positions);
    }

    [Fact]
    public async Task Daily_loss_limit_blocks_firing_but_keeps_rule_active()
    {
        await CreateRule();
        // Lỗ hôm nay 60 USDT > 5% của 1000 USDT.
        _paperStore.Ledger.Add(new PaperLedgerEntry(-60m, LedgerEntryType.RealizedPnl, null, "test", T0));
        SetMark(97_900m);

        var risk = await _svc.GetRiskAsync(default);
        Assert.True(risk.Blocked);

        Assert.Equal(0, await _svc.EvaluateAsync(default));

        var rule = _ruleStore.Rules.Single();
        Assert.Equal(AutoRuleStatus.Active, rule.Status);
        Assert.Contains("Bị chặn", rule.Note);
        Assert.Empty(_paperStore.Positions);
    }

    [Fact]
    public async Task Loss_from_previous_day_does_not_block_today()
    {
        // Vốn nạp từ 2 ngày trước, lỗ 300 USDT hôm qua.
        _paperStore.Ledger.Add(new PaperLedgerEntry(1000m, LedgerEntryType.Deposit, null, "nạp",
            T0 - TimeSpan.FromDays(2)));
        _paperStore.Ledger.Add(new PaperLedgerEntry(-300m, LedgerEntryType.RealizedPnl, null, "hôm qua",
            T0 - TimeSpan.FromDays(1)));

        var risk = await _svc.GetRiskAsync(default);

        Assert.False(risk.Blocked);
        Assert.Equal(0m, risk.TodayRealizedPnl);
        // 1000 nạp 2 ngày trước − 300 lỗ hôm qua (khoản nạp tự động hôm nay thuộc ngày hôm nay).
        Assert.Equal(700m, risk.DayStartEquity);
    }

    [Fact]
    public async Task Stale_mark_price_never_fires_a_rule()
    {
        await CreateRule();
        SetMark(97_900m);
        _clock.UtcNow = T0 + TimeSpan.FromMinutes(5);

        Assert.Equal(0, await _svc.EvaluateAsync(default));

        Assert.Empty(_paperStore.Positions);
    }

    [Fact]
    public async Task Cancelled_rule_never_fires()
    {
        var rule = await CreateRule();
        Assert.True(await _svc.CancelAsync(rule.Id, default));
        SetMark(97_900m);

        await _svc.EvaluateAsync(default);

        Assert.Empty(_paperStore.Positions);
        Assert.False(await _svc.CancelAsync(rule.Id, default));
    }

    [Theory]
    [InlineData(101_000, "đã thỏa điều kiện")]          // giá hiện tại 100000 đã ≤ 101000
    public async Task Create_rejects_trigger_that_is_already_true(decimal trigger, string expected)
    {
        var result = await _svc.CreateAsync(BuyDip(trigger: trigger), default);

        Assert.Null(result.Rule);
        Assert.Contains(result.Errors, e => e.Contains(expected));
    }

    [Fact]
    public async Task Create_validates_inputs()
    {
        Assert.Contains((await _svc.CreateAsync(BuyDip(leverage: 20), default)).Errors, e => e.Contains("Đòn bẩy"));
        // Margin quá nhỏ: ở giá ~98000 không đủ mua một bước khối lượng (bước 0.001 BTC chặt hơn min notional 50).
        Assert.Contains((await _svc.CreateAsync(BuyDip(margin: 10m, leverage: 2), default)).Errors,
            e => e.Contains("không đủ mua"));
        Assert.Contains((await _svc.CreateAsync(BuyDip(sl: 0m), default)).Errors, e => e.Contains("Stop loss"));
        // Ở 98000: 50 × 2 / 98000 = 0.00102 → đủ 0.001 BTC; ở 120000 thì làm tròn về 0.
        Assert.Contains((await _svc.CreateAsync(BuyDip(trigger: 120_000m, cond: TriggerCondition.AtOrAbove), default)).Errors,
            e => e.Contains("không đủ mua"));
        // 10x: giá thanh lý cách ~9.6%, stop 15% nằm sau giá thanh lý.
        Assert.Contains((await _svc.CreateAsync(BuyDip(leverage: 10, sl: 15m), default)).Errors,
            e => e.Contains("thanh lý"));
        Assert.Contains((await _svc.CreateAsync(BuyDip() with { Symbol = "DOGEUSDT" }, default)).Errors,
            e => e.Contains("chưa được cấu hình"));
    }

    [Fact]
    public async Task Create_enforces_max_active_rules()
    {
        _options.MaxActiveRules = 2;
        await CreateRule();
        await CreateRule();

        var third = await _svc.CreateAsync(BuyDip(), default);

        Assert.Null(third.Rule);
        Assert.Contains(third.Errors, e => e.Contains("tối đa 2"));
    }
}
