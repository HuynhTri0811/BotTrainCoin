using CryptoBot.Application.Backtesting;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.Backtesting;
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
}
