using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Futures;
using CryptoBot.Application.Indicators;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Futures;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.Application.Tests;

internal sealed class InMemoryAutoOrderStore : IAutoOrderStore
{
    public readonly List<AutoOrderRule> Rules = [];
    private long _nextId = 1;

    public Task<IReadOnlyList<AutoOrderRule>> GetActiveAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AutoOrderRule>>(Rules.Where(rule => rule.Status == AutoRuleStatus.Active)
            .ToList());

    public Task<IReadOnlyList<AutoOrderRule>> GetWatchingCloseAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AutoOrderRule>>(Rules.Where(rule => rule.IsWatchingClose).ToList());

    public Task<IReadOnlyList<AutoOrderRule>> GetRecentAsync(int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AutoOrderRule>>(Rules.AsEnumerable().Reverse().Take(limit).ToList());

    public Task<AutoOrderRule?> GetAsync(long id, CancellationToken ct) =>
        Task.FromResult(Rules.FirstOrDefault(rule => rule.Id == id));

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
    private readonly InMemoryCandleStore _candles = new();
    private readonly AutoOrderService _svc;

    public AutoOrderServiceTests()
    {
        _marks = new MarkPriceCache(_clock, _paperOptions);
        _paper = new PaperFuturesService(_paperStore, _marks, new PaperFuturesLock(), _paperOptions, _clock,
            NullLogger<PaperFuturesService>.Instance);
        // RSI(2) trên 5 nến 1h: dễ dựng chuỗi giá cắt lên 30 / cắt xuống 70.
        var registry = new IndicatorRegistry(
        [
            new IndicatorDefinition
            {
                Name = "rsi-test", Type = "MyRsi", Symbol = "BTCUSDT", Interval = "1h",
                Parameters = new() { ["Period"] = "2", ["LookbackCandles"] = "5" },
            },
        ], NullLogger<IndicatorRegistry>.Instance);
        _svc = new AutoOrderService(_ruleStore, _paperStore, _paper, _marks, new IndicatorService(registry, _candles),
            registry, _paperOptions, _options, _clock, NullLogger<AutoOrderService>.Instance);
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

        var position = Assert.Single(_paperStore.Positions);
        Assert.Equal(PositionSide.Long, position.Side);
        Assert.Equal(2, position.Leverage);
        Assert.Equal(96_921m, position.StopLoss);        // 97900 * 0.99
        Assert.Equal(99_858m, position.TakeProfit);      // 97900 * 1.02
        Assert.Equal(AutoRuleStatus.Triggered, rule.Status);
        Assert.Equal(position.Id, rule.PositionId);
        Assert.Equal(97_900m, rule.TriggeredMarkPrice);
    }

    [Fact]
    public async Task Short_rule_above_trigger_places_stop_above_and_take_profit_below()
    {
        await CreateRule(BuyDip(trigger: 102_000m, margin: 100m, side: PositionSide.Short, cond: TriggerCondition.AtOrAbove));
        SetMark(102_500m);

        await _svc.EvaluateAsync(default);

        var position = Assert.Single(_paperStore.Positions);
        Assert.Equal(PositionSide.Short, position.Side);
        Assert.Equal(103_525m, position.StopLoss);       // 102500 * 1.01
        Assert.Equal(100_450m, position.TakeProfit);     // 102500 * 0.98
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
        Assert.Contains(result.Errors, error => error.Contains(expected));
    }

    [Fact]
    public async Task Create_validates_inputs()
    {
        Assert.Contains((await _svc.CreateAsync(BuyDip(leverage: 20), default)).Errors,
            error => error.Contains("Đòn bẩy"));
        // Margin quá nhỏ: ở giá ~98000 không đủ mua một bước khối lượng (bước 0.001 BTC chặt hơn min notional 50).
        Assert.Contains((await _svc.CreateAsync(BuyDip(margin: 10m, leverage: 2), default)).Errors,
            error => error.Contains("không đủ mua"));
        Assert.Contains((await _svc.CreateAsync(BuyDip(sl: 0m), default)).Errors, error => error.Contains("Stop loss"));
        // Ở 98000: 50 × 2 / 98000 = 0.00102 → đủ 0.001 BTC; ở 120000 thì làm tròn về 0.
        Assert.Contains((await _svc.CreateAsync(BuyDip(trigger: 120_000m, cond: TriggerCondition.AtOrAbove), default)).Errors,
            error => error.Contains("không đủ mua"));
        // 10x: giá thanh lý cách ~9.6%, stop 15% nằm sau giá thanh lý.
        Assert.Contains((await _svc.CreateAsync(BuyDip(leverage: 10, sl: 15m), default)).Errors,
            error => error.Contains("thanh lý"));
        Assert.Contains((await _svc.CreateAsync(BuyDip() with { Symbol = "DOGEUSDT" }, default)).Errors,
            error => error.Contains("chưa được cấu hình"));
    }

    [Fact]
    public async Task Create_enforces_max_active_rules()
    {
        _options.MaxActiveRules = 2;
        await CreateRule();
        await CreateRule();

        var third = await _svc.CreateAsync(BuyDip(), default);

        Assert.Null(third.Rule);
        Assert.Contains(third.Errors, error => error.Contains("tối đa 2"));
    }

    // ── Quy tắc theo chỉ báo ──────────────────────────────────────────────────

    private static readonly decimal[] RsiCrossUp = [100m, 99m, 98m, 97m, 110m];   // RSI 0 → ~92.9: Long
    private static readonly decimal[] RsiCrossDown = [100m, 101m, 102m, 103m, 90m]; // RSI 100 → ~7.1: Short

    /// <summary>Thêm các nến 1h đã đóng, nến cuối mở lúc <paramref name="lastOpen"/>.</summary>
    private void AddCandles(DateTime lastOpen, decimal[] closes)
    {
        for (var index = 0; index < closes.Length; index++)
        {
            var open = lastOpen - TimeSpan.FromHours(closes.Length - 1 - index);
            _candles.Candles[open] = new Candle("Binance", "BTCUSDT", CandleInterval.H1, open,
                closes[index], closes[index], closes[index], closes[index], 1m, true, open);
        }
    }

    private static CreateRuleRequest OnIndicator(PositionSide side = PositionSide.Long, string name = "rsi-test",
        string symbol = "BTCUSDT", bool closeOnReverse = false) =>
        new(symbol, side, null, null, 50m, 2, 1m, 2m, RuleTriggerType.Indicator, name, closeOnReverse);

    private void AdvanceTo(DateTime now)
    {
        _clock.UtcNow = now;
        SetMark(100_000m);
    }

    [Fact]
    public async Task Indicator_rule_fires_on_matching_signal_from_candle_closed_after_creation()
    {
        var rule = await CreateRule(OnIndicator());
        AddCandles(T0, RsiCrossUp);                 // nến mở 12:00, đóng 13:00 (sau lúc tạo 12:00)
        AdvanceTo(T0.AddHours(1).AddSeconds(30));

        Assert.Equal(1, await _svc.EvaluateAsync(default));

        var position = Assert.Single(_paperStore.Positions);
        Assert.Equal(PositionSide.Long, position.Side);
        Assert.Equal(99_000m, position.StopLoss);           // SL/TP theo mark lúc kích hoạt, như quy tắc theo giá
        Assert.Equal(AutoRuleStatus.Triggered, rule.Status);
        Assert.Contains("rsi-test", rule.TriggerDetail);
        Assert.Contains("RSI cắt lên", rule.TriggerDetail);
    }

    [Fact]
    public async Task Indicator_rule_ignores_signal_on_candle_closed_before_creation()
    {
        AddCandles(T0.AddHours(-1), RsiCrossUp);    // đóng đúng 12:00 = lúc tạo quy tắc
        await CreateRule(OnIndicator());
        AdvanceTo(T0.AddMinutes(1));

        Assert.Equal(0, await _svc.EvaluateAsync(default));
        Assert.Empty(_paperStore.Positions);
    }

    [Fact]
    public async Task Indicator_rule_ignores_stale_candles()
    {
        await CreateRule(OnIndicator());
        AddCandles(T0, RsiCrossUp);
        // Nến 13:00–14:00 đáng lẽ đã đóng và được đồng bộ; dữ liệu đang trễ.
        AdvanceTo(T0.AddHours(2) + AutoOrderService.IndicatorStaleGrace + TimeSpan.FromSeconds(1));

        Assert.Equal(0, await _svc.EvaluateAsync(default));
    }

    [Fact]
    public async Task Indicator_rule_ignores_opposite_signal()
    {
        var rule = await CreateRule(OnIndicator(PositionSide.Short));
        AddCandles(T0, RsiCrossUp);
        AdvanceTo(T0.AddHours(1).AddSeconds(30));

        Assert.Equal(0, await _svc.EvaluateAsync(default));
        Assert.Equal(AutoRuleStatus.Active, rule.Status);
    }

    [Fact]
    public async Task Indicator_short_rule_fires_on_short_signal()
    {
        await CreateRule(OnIndicator(PositionSide.Short));
        AddCandles(T0, RsiCrossDown);
        AdvanceTo(T0.AddHours(1).AddSeconds(30));

        Assert.Equal(1, await _svc.EvaluateAsync(default));
        Assert.Equal(PositionSide.Short, Assert.Single(_paperStore.Positions).Side);
    }

    [Fact]
    public async Task Indicator_rule_respects_daily_loss_limit()
    {
        var rule = await CreateRule(OnIndicator());
        _paperStore.Ledger.Add(new PaperLedgerEntry(-60m, LedgerEntryType.RealizedPnl, null, "test", T0));
        AddCandles(T0, RsiCrossUp);
        AdvanceTo(T0.AddHours(1).AddSeconds(30));

        Assert.Equal(0, await _svc.EvaluateAsync(default));
        Assert.Equal(AutoRuleStatus.Active, rule.Status);
        Assert.Contains("Bị chặn", rule.Note);
    }

    [Fact]
    public async Task Create_indicator_rule_validates_indicator_and_symbol()
    {
        Assert.Contains((await _svc.CreateAsync(OnIndicator(name: "nope"), default)).Errors,
            error => error.Contains("Không có chỉ báo"));
        _paperOptions.Symbols.Add(new FuturesSymbolOptions { Symbol = "ETHUSDT", QuantityStep = 0.01m, TickSize = 0.01m, MinNotional = 20m, MaintenanceMarginRate = 0.005m });
        _marks.Set(new MarkPrice("ETHUSDT", 3000m, _clock.UtcNow));
        Assert.Contains((await _svc.CreateAsync(OnIndicator(symbol: "ETHUSDT"), default)).Errors,
            error => error.Contains("chạy trên BTCUSDT"));

        var ok = await _svc.CreateAsync(OnIndicator(), default);
        Assert.Empty(ok.Errors);
        Assert.Equal(RuleTriggerType.Indicator, ok.Rule!.TriggerType);
        Assert.Null(ok.Rule.TriggerPrice);
    }

    // ── Tự đóng khi chỉ báo đảo chiều ─────────────────────────────────────────

    /// <summary>Tạo quy tắc Long theo chỉ báo, mở lệnh trên nến 12:00–13:00 (RSI cắt lên).</summary>
    private async Task<AutoOrderRule> OpenLongOnIndicator(bool closeOnReverse)
    {
        var rule = await CreateRule(OnIndicator(closeOnReverse: closeOnReverse));
        AddCandles(T0, RsiCrossUp);
        AdvanceTo(T0.AddHours(1).AddSeconds(30));
        Assert.Equal(1, await _svc.EvaluateAsync(default));
        return rule;
    }

    [Fact]
    public async Task Close_on_reverse_closes_long_when_indicator_signals_short()
    {
        var rule = await OpenLongOnIndicator(closeOnReverse: true);
        AddCandles(T0.AddHours(1), RsiCrossDown);   // nến 13:00–14:00 báo Short
        AdvanceTo(T0.AddHours(2).AddSeconds(30));

        Assert.Equal(1, await _svc.EvaluateAsync(default));

        var position = Assert.Single(_paperStore.Positions);
        Assert.Equal(PositionStatus.Closed, position.Status);
        Assert.Equal(CloseReason.Signal, position.CloseReason);
        Assert.Equal(rule.CloseCommandId, position.CloseCommandId);
        Assert.False(rule.IsWatchingClose);
        Assert.Contains("RSI cắt xuống", rule.CloseDetail);

        // Đã ngừng theo dõi: lần kiểm tra sau không làm gì thêm.
        Assert.Equal(0, await _svc.EvaluateAsync(default));
    }

    [Fact]
    public async Task Close_on_reverse_ignores_signal_in_same_direction()
    {
        var rule = await OpenLongOnIndicator(closeOnReverse: true);
        AddCandles(T0.AddHours(1), RsiCrossUp);
        AdvanceTo(T0.AddHours(2).AddSeconds(30));

        Assert.Equal(0, await _svc.EvaluateAsync(default));
        Assert.Equal(PositionStatus.Open, Assert.Single(_paperStore.Positions).Status);
        Assert.True(rule.IsWatchingClose);
    }

    [Fact]
    public async Task Without_close_on_reverse_position_stays_open_on_opposite_signal()
    {
        var rule = await OpenLongOnIndicator(closeOnReverse: false);
        AddCandles(T0.AddHours(1), RsiCrossDown);
        AdvanceTo(T0.AddHours(2).AddSeconds(30));

        Assert.Equal(0, await _svc.EvaluateAsync(default));
        Assert.Equal(PositionStatus.Open, Assert.Single(_paperStore.Positions).Status);
        Assert.Null(rule.CloseCommandId);
    }

    [Fact]
    public async Task Close_on_reverse_stops_watching_when_stop_loss_closed_first()
    {
        var rule = await OpenLongOnIndicator(closeOnReverse: true);
        SetMark(98_900m);                           // dưới SL 99 000
        Assert.Equal(1, await _paper.EvaluateTriggersAsync(default));

        Assert.Equal(0, await _svc.EvaluateAsync(default));
        Assert.False(rule.IsWatchingClose);
        Assert.Contains("StopLoss", rule.CloseDetail);
    }

    [Fact]
    public async Task Close_on_reverse_waits_for_fresh_mark_price()
    {
        var rule = await OpenLongOnIndicator(closeOnReverse: true);
        AddCandles(T0.AddHours(1), RsiCrossDown);
        _clock.UtcNow = T0.AddHours(2).AddSeconds(30); // mark price cũ đã hết hạn

        Assert.Equal(0, await _svc.EvaluateAsync(default));
        Assert.Equal(PositionStatus.Open, Assert.Single(_paperStore.Positions).Status);
        Assert.True(rule.IsWatchingClose);

        SetMark(100_000m);
        Assert.Equal(1, await _svc.EvaluateAsync(default));
        Assert.Equal(CloseReason.Signal, Assert.Single(_paperStore.Positions).CloseReason);
    }

    [Fact]
    public async Task Create_rejects_close_on_reverse_for_price_rule()
    {
        var result = await _svc.CreateAsync(BuyDip() with { CloseOnReverse = true }, default);

        Assert.Null(result.Rule);
        Assert.Contains(result.Errors, error => error.Contains("chỉ dùng với quy tắc theo chỉ báo"));
    }
}
