using CryptoBot.Application.Backtesting;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.Futures;
using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Tests;

public class BacktestServiceTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly BacktestSettings Settings =
        new(1000m, PositionSizing.PercentOfEquity, 100m, null, null, 0m);

    // BUY ở nến 3, SELL ở nến 7 với EMA 2/3 trên cửa sổ 4 nến.
    private static readonly decimal[] UpThenDown = [10, 10, 10, 13, 14, 15, 16, 8, 7, 7];

    private readonly InMemoryCandleStore _candles = new();
    private readonly InMemoryStrategyInstanceStore _instances = new();

    public BacktestServiceTests()
    {
        var instance = new StrategyInstance("test", StrategyTypes.EmaCross,
            StrategyFactory.Serialize(new EmaCrossParameters(2, 3, 4)), "Binance", "BTCUSDT", "1h", true);
        typeof(StrategyInstance).GetProperty(nameof(StrategyInstance.Id))!.SetValue(instance, 1);
        _instances.Instances.Add(instance);
    }

    private BacktestService CreateService() => new(_instances, _candles);

    private static DateTime Hour(int index) => T0 + TimeSpan.FromHours(index);

    private void AddCandles(decimal[] closes, bool lastIsRunning = false)
    {
        for (var index = 0; index < closes.Length; index++)
        {
            var close = closes[index];
            var closed = !(lastIsRunning && index == closes.Length - 1);
            _candles.Candles[Hour(index)] = new Candle("Binance", "BTCUSDT", CandleInterval.H1, Hour(index), close,
                close, close, close, 1, closed, T0);
        }
    }

    [Fact]
    public async Task Runs_the_instance_strategy_over_stored_closed_candles()
    {
        AddCandles([.. UpThenDown, 7], lastIsRunning: true);

        var run = await CreateService().RunAsync(new BacktestRequest(1, null, null, null, Settings),
            CancellationToken.None);

        Assert.NotNull(run);
        Assert.Equal(10, run.CandleCount);
        Assert.Equal(Hour(9), run.LastCandleUtc);
        var trade = Assert.Single(run.Result.Trades);
        Assert.Equal(14m, trade.EntryPrice);
        Assert.Equal(7m, trade.ExitPrice);
        Assert.Equal(10, run.Chart.Bars.Count);
    }

    [Fact]
    public async Task Range_start_uses_earlier_candles_as_warm_up()
    {
        AddCandles(UpThenDown);

        // Bắt đầu ở nến 3: 3 nến trước đó chỉ làm warm-up nên BUY ở nến 3 vẫn được phát.
        var run = await CreateService().RunAsync(new BacktestRequest(1, Hour(3), Hour(8), null, Settings),
            CancellationToken.None);

        Assert.Equal(6, run!.CandleCount);
        Assert.Equal(3, run.WarmupCandles);
        Assert.Equal(Hour(3), run.Result.Signals[0].CandleOpenTimeUtc);
        Assert.Single(run.Result.Trades);
    }

    [Fact]
    public async Task Parameters_override_the_instance_parameters()
    {
        AddCandles(UpThenDown);

        // Cửa sổ 9 nến: chỉ đủ cửa sổ từ nến 8, sau các giao cắt.
        var run = await CreateService().RunAsync(
            new BacktestRequest(1, null, null, new EmaCrossOverrides(2, 3, 9), Settings), CancellationToken.None);

        Assert.Equal(9, run!.Parameters.LookbackCandles);
        Assert.Empty(run.Result.Signals);
    }

    [Fact]
    public async Task Chart_groups_candles_when_there_are_too_many()
    {
        AddCandles(Enumerable.Repeat(10m, BacktestService.MaxChartBars * 2 + 1).ToArray());

        var run = await CreateService().RunAsync(new BacktestRequest(1, null, null, null, Settings),
            CancellationToken.None);

        // 6001 nến, tối đa 3000 cột → 3 nến một cột → 2001 cột.
        Assert.Equal(3, run!.Chart.CandlesPerBar);
        Assert.Equal(2001, run.Chart.Bars.Count);
    }

    [Fact]
    public async Task Coverage_reports_the_closed_candle_range()
    {
        AddCandles(UpThenDown, lastIsRunning: true);

        var coverage = await CreateService().GetCoverageAsync(1, CancellationToken.None);

        Assert.Equal(new BacktestCoverage(Hour(0), Hour(8)), coverage);
    }

    [Fact]
    public async Task Unknown_strategy_returns_null()
    {
        Assert.Null(await CreateService().RunAsync(new BacktestRequest(99, null, null, null, Settings),
            CancellationToken.None));
        Assert.Null(await CreateService().GetCoverageAsync(99, CancellationToken.None));
    }

    [Fact]
    public async Task Invalid_input_is_rejected()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.RunAsync(
            new BacktestRequest(1, Hour(5), Hour(1), null, Settings), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RunAsync(
            new BacktestRequest(1, null, null, new EmaCrossOverrides(5, 3, 10), Settings), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RunAsync(
            new BacktestRequest(1, null, null, null, Settings with { InitialCapital = 0 }), CancellationToken.None));
    }

    [Fact]
    public async Task Candle_limit_holds_even_when_the_range_ends_inside_a_page()
    {
        // Một nến quá giới hạn trong khoảng, và một nến sau ToUtc cùng trang: trước đây trả về sớm không báo lỗi.
        AddCandles(Enumerable.Repeat(10m, BacktestService.MaxCandles + 2).ToArray());

        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().RunAsync(
            new BacktestRequest(1, null, Hour(BacktestService.MaxCandles), null, Settings), CancellationToken.None));
    }

    [Fact]
    public async Task Overrides_left_null_keep_the_instance_parameters()
    {
        AddCandles(UpThenDown);

        var run = await CreateService().RunAsync(
            new BacktestRequest(1, null, null, new EmaCrossOverrides(1, null, null), Settings), CancellationToken.None);

        Assert.Equal(new EmaCrossParameters(1, 3, 4), run!.Parameters);
    }

    [Fact]
    public async Task Lookback_above_the_backtest_limit_is_rejected()
    {
        AddCandles(UpThenDown);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => CreateService().RunAsync(
            new BacktestRequest(1, null, null,
                new EmaCrossOverrides(null, null, BacktestService.MaxLookbackCandles + 1), Settings),
            CancellationToken.None));
        Assert.Contains("LookbackCandles", error.Message);
    }

    [Fact]
    public async Task Other_strategy_types_and_broken_parameters_are_rejected_as_bad_input()
    {
        AddCandles(UpThenDown);
        AddInstance(2, "Rsi", StrategyFactory.Serialize(new EmaCrossParameters(2, 3, 4)));
        AddInstance(3, StrategyTypes.EmaCross, "{not json");

        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().RunAsync(
            new BacktestRequest(2, null, null, null, Settings), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().RunAsync(
            new BacktestRequest(3, null, null, null, Settings), CancellationToken.None));
    }

    [Fact]
    public async Task Range_without_candles_is_rejected_so_nothing_is_saved()
    {
        AddCandles(UpThenDown);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().RunAsync(
            new BacktestRequest(1, Hour(50), null, null, Settings), CancellationToken.None));
    }

    [Fact]
    public async Task Cancelled_request_stops_the_run()
    {
        AddCandles(UpThenDown);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().RunAsync(
            new BacktestRequest(1, null, null, null, Settings), cancelled.Token));
    }

    private void AddInstance(int id, string strategyType, string parametersJson)
    {
        var instance = new StrategyInstance($"test-{id}", strategyType, parametersJson, "Binance", "BTCUSDT", "1h",
            true);
        typeof(StrategyInstance).GetProperty(nameof(StrategyInstance.Id))!.SetValue(instance, id);
        _instances.Instances.Add(instance);
    }

    [Fact]
    public async Task Chart_bars_carry_the_ema_the_bot_used_for_its_signals()
    {
        AddCandles(UpThenDown);

        var run = await CreateService().RunAsync(new BacktestRequest(1, null, null, null, Settings),
            CancellationToken.None);

        var bars = run!.Chart.Bars;
        // Cửa sổ 4 nến: 3 cột đầu chưa đủ nến nên chưa có EMA.
        Assert.All(bars.Take(3), bar => Assert.Null(bar.EmaFast));
        var expected = new EmaCrossStrategy(new EmaCrossParameters(2, 3, 4)).ComputeAt(UpThenDown.AsSpan(0, 4))!.Value;
        Assert.Equal(expected.Fast, bars[3].EmaFast);
        Assert.Equal(expected.Slow, bars[3].EmaSlow);
        // BUY ở nến 3: EMA nhanh vừa vượt EMA chậm.
        Assert.True(bars[3].EmaFast > bars[3].EmaSlow);
    }

    // 1x: lệnh Long ở ví dụ EMA lỗ một nửa, vẫn còn tiền để mở Short.
    private static readonly BacktestSettings FuturesBoth = Settings with
    {
        Futures = new FuturesBacktestSettings(1, BacktestDirection.Both, true, 0m, 0.4m),
    };

    /// <summary>Giá dao động mạnh để RSI cắt qua cả mức quá bán lẫn quá mua nhiều lần.</summary>
    private static decimal[] Waves(int count) => Enumerable.Range(0, count)
        .Select(index => Math.Round(100m + 20m * (decimal)Math.Sin(index / 4.0), 4))
        .ToArray();

    private static readonly Dictionary<string, string?> SmallRsi = new()
    {
        ["Period"] = "5",
        ["LookbackCandles"] = "20",
    };

    [Fact]
    public async Task Futures_ema_cross_opens_short_on_the_downward_cross()
    {
        AddCandles(UpThenDown);

        var run = await CreateService().RunAsync(new BacktestRequest(1, null, null, null, FuturesBoth),
            CancellationToken.None);

        var trade = Assert.Single(run!.Result.Trades);
        Assert.Equal(PositionSide.Long, trade.Side);
        Assert.Equal(PositionSide.Short, run.Result.OpenTrade!.Side);
        Assert.Equal(StrategyTypes.EmaCross, run.Signal.Type);
        Assert.Equal(new EmaCrossParameters(2, 3, 4), run.Parameters);
        Assert.Contains(run.Chart.Bars, bar => bar.EmaFast is not null);
    }

    [Fact]
    public async Task Futures_can_trade_a_custom_indicator_on_the_instance_candles()
    {
        AddCandles(Waves(200));

        var run = await CreateService().RunAsync(
            new BacktestRequest(1, Hour(30), null, null, FuturesBoth, "myrsi", SmallRsi), CancellationToken.None);

        Assert.NotNull(run);
        Assert.Equal("MyRsi", run.Signal.Type);
        Assert.Equal("5", run.Signal.Parameters["Period"]);
        Assert.Equal("70", run.Signal.Parameters["Overbought"]);
        Assert.Equal(20, run.Signal.RequiredCandles);
        Assert.Null(run.Parameters);
        Assert.Equal(19, run.WarmupCandles);
        Assert.Contains(run.Result.Trades, trade => trade.Side == PositionSide.Long);
        Assert.Contains(run.Result.Trades, trade => trade.Side == PositionSide.Short);
        Assert.All(run.Chart.Bars, bar => Assert.Null(bar.EmaFast));
    }

    [Fact]
    public async Task Small_macd_trades_in_the_futures_backtest()
    {
        AddCandles(Waves(200));
        var run = await CreateService().RunAsync(new BacktestRequest(1, null, null, null, FuturesBoth, "macd",
            new Dictionary<string, string?>
            {
                ["FastPeriod"] = "3", ["SlowPeriod"] = "8", ["SignalPeriod"] = "3", ["LookbackCandles"] = "30",
            }), CancellationToken.None);

        Assert.Equal("Macd", run!.Signal.Type);
        Assert.Equal(30, run.Signal.RequiredCandles);
        Assert.NotEmpty(run.Result.Trades);
    }

    [Fact]
    public async Task Default_macd_reads_its_full_warm_up_and_trades()
    {
        Assert.InRange(new MacdIndicator().RequiredCandles, 1, BacktestService.MaxLookbackCandles);
        // Sóng chậm (chu kỳ khoảng 500 nến) để MACD 96/208/72 kịp cắt đường tín hiệu nhiều lần.
        AddCandles(Enumerable.Range(0, 2600)
            .Select(index => Math.Round(100m + 20m * (decimal)Math.Sin(index / 80.0), 4))
            .ToArray());

        var run = await CreateService().RunAsync(
            new BacktestRequest(1, Hour(600), null, null, FuturesBoth, "Macd"), CancellationToken.None);

        Assert.Equal(new Dictionary<string, string>
        {
            ["FastPeriod"] = "96", ["SlowPeriod"] = "208", ["SignalPeriod"] = "72", ["LookbackCandles"] = "600",
        }, run!.Signal.Parameters);
        Assert.Equal(600, run.Signal.RequiredCandles);
        Assert.Equal(599, run.WarmupCandles);
        Assert.Equal(2000, run.CandleCount);
        Assert.Contains(run.Result.Trades, trade => trade.Side == PositionSide.Long);
        Assert.Contains(run.Result.Trades, trade => trade.Side == PositionSide.Short);
    }

    [Fact]
    public async Task Indicator_runs_do_not_need_an_ema_instance()
    {
        AddCandles(Waves(60));
        AddInstance(2, "Rsi", "{not json");

        var run = await CreateService().RunAsync(
            new BacktestRequest(2, null, null, null, FuturesBoth, "MyRsi", SmallRsi), CancellationToken.None);

        Assert.Equal("MyRsi", run!.Signal.Type);
    }

    [Fact]
    public async Task Indicators_need_futures_and_a_known_type()
    {
        AddCandles(Waves(60));
        var service = CreateService();

        var spot = await Assert.ThrowsAsync<ArgumentException>(() => service.RunAsync(
            new BacktestRequest(1, null, null, null, Settings, "MyRsi", SmallRsi), CancellationToken.None));
        var unknown = await Assert.ThrowsAsync<ArgumentException>(() => service.RunAsync(
            new BacktestRequest(1, null, null, null, FuturesBoth, "Nope"), CancellationToken.None));
        var badParameter = await Assert.ThrowsAsync<ArgumentException>(() => service.RunAsync(
            new BacktestRequest(1, null, null, null, FuturesBoth, "MyRsi",
                new Dictionary<string, string?> { ["Period"] = "abc" }), CancellationToken.None));

        Assert.Contains("Futures", spot.Message);
        Assert.Contains("Không có chỉ báo loại 'Nope'", unknown.Message);
        Assert.Contains("Period", badParameter.Message);
    }
}
