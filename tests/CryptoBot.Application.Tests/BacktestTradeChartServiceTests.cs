using CryptoBot.Application.Backtesting;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Tests;

public class BacktestTradeChartServiceTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly BacktestSettings Settings = new(1000m, PositionSizing.PercentOfEquity, 10m, null, null, 0m);

    // 40 nến đứng yên rồi BUY ở nến 43, SELL ở nến 47 (EMA 2/3, cửa sổ 4): lệnh vào ở nến 44, ra ở nến 48.
    private static readonly decimal[] Closes = [.. Enumerable.Repeat(10m, 40), 10, 10, 10, 13, 14, 15, 16, 8, 7, 7];

    private readonly InMemoryCandleStore _candles = new();
    private readonly InMemoryStrategyInstanceStore _instances = new();
    private readonly InMemoryBacktestStore _store = new();
    private readonly FakeClock _clock = new(T0.AddDays(30));

    public BacktestTradeChartServiceTests()
    {
        var instance = new StrategyInstance("test", StrategyTypes.EmaCross,
            StrategyFactory.Serialize(new EmaCrossParameters(2, 3, 4)), "Binance", "BTCUSDT", "1h", true);
        typeof(StrategyInstance).GetProperty(nameof(StrategyInstance.Id))!.SetValue(instance, 1);
        _instances.Instances.Add(instance);
        for (var index = 0; index < Closes.Length; index++)
            _candles.Candles[Hour(index)] = new Candle("Binance", "BTCUSDT", CandleInterval.H1, Hour(index),
                Closes[index], Closes[index], Closes[index], Closes[index], 1, true, T0);
    }

    private static DateTime Hour(int index) => T0 + TimeSpan.FromHours(index);

    private BacktestHistoryService History() => new(_store, _clock);

    private BacktestTradeChartService CreateService() => new(History(), _instances, _candles);

    private async Task<(long Id, BacktestTrade Trade)> SaveRunAsync()
    {
        var run = await new BacktestService(_instances, _candles).RunAsync(
            new BacktestRequest(1, null, null, null, Settings), CancellationToken.None);
        var id = (await History().SaveAsync(run!, CancellationToken.None)).Saved.Id;
        return (id, Assert.Single(run!.Result.Trades));
    }

    [Fact]
    public async Task Shows_real_candles_from_before_entry_to_after_exit_with_ema()
    {
        var (id, trade) = await SaveRunAsync();

        var chart = await CreateService().GetAsync(id, trade.EntryTimeUtc, trade.ExitTimeUtc, CancellationToken.None);

        Assert.NotNull(chart);
        Assert.Equal(1, chart.CandlesPerBar);
        // Vào ở nến 44: bắt đầu 30 nến trước; hết dữ liệu ở nến 49 (sau lúc ra ở nến 48).
        Assert.Equal(Hour(44), trade.EntryTimeUtc);
        Assert.Equal(Hour(44 - BacktestTradeChartService.ContextCandles), chart.Bars[0].OpenTimeUtc);
        Assert.Equal(Hour(Closes.Length - 1), chart.Bars[^1].OpenTimeUtc);
        // Nến trước khoảng hiển thị dùng làm warm-up, nên cột đầu đã có EMA.
        Assert.All(chart.Bars, bar => Assert.NotNull(bar.EmaFast));
        Assert.All(chart.Bars, bar => Assert.Null(bar.Equity));
    }

    [Fact]
    public async Task Trade_held_longer_than_fifty_thousand_candles_still_reaches_its_exit()
    {
        // BUY ở nến 43 rồi giá chỉ tăng: lệnh còn mở tới nến cuối, giữ hơn 60.000 nến.
        const int length = 60_100;
        for (var index = Closes.Length; index < length; index++)
        {
            var close = 16m + index * 0.001m;
            _candles.Candles[Hour(index)] = new Candle("Binance", "BTCUSDT", CandleInterval.H1, Hour(index), close,
                close, close, close, 1, true, T0);
        }

        var run = await new BacktestService(_instances, _candles).RunAsync(
            new BacktestRequest(1, null, null, null, Settings), CancellationToken.None);
        var id = (await History().SaveAsync(run!, CancellationToken.None)).Saved.Id;
        var open = run!.Result.OpenTrade!;

        var chart = await CreateService().GetAsync(id, open.EntryTimeUtc, run.LastCandleUtc!.Value,
            CancellationToken.None);

        // Cột cuối chứa nến cuối của lần chạy, nơi lệnh còn mở được định giá.
        var lastBar = chart!.Bars[^1];
        Assert.True(lastBar.OpenTimeUtc + TimeSpan.FromHours(chart.CandlesPerBar) > Hour(length - 1));
        Assert.True(chart.Bars.Count <= BacktestTradeChartService.MaxBars);
    }

    [Fact]
    public async Task Ema_is_left_out_where_the_window_spans_missing_candles()
    {
        var (id, trade) = await SaveRunAsync();
        _candles.Candles.Remove(Hour(30));

        var chart = await CreateService().GetAsync(id, trade.EntryTimeUtc, trade.ExitTimeUtc, CancellationToken.None);

        // Cửa sổ 4 nến kết thúc ở nến 31, 32, 33 chứa khoảng thiếu nến 30: bot không đánh giá, chart không vẽ EMA.
        var emptyEma = chart!.Bars.Where(bar => bar.EmaFast is null).Select(bar => bar.OpenTimeUtc);
        Assert.Equal([Hour(31), Hour(32), Hour(33)], emptyEma);
    }

    [Fact]
    public async Task Unknown_run_is_null()
    {
        Assert.Null(await CreateService().GetAsync(999, Hour(44), Hour(48), CancellationToken.None));
    }

    [Fact]
    public async Task Times_outside_the_run_or_reversed_are_rejected()
    {
        var (id, _) = await SaveRunAsync();
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAsync(id, Hour(48), Hour(44), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAsync(id, Hour(44), Hour(Closes.Length + 5), CancellationToken.None));
    }

    [Fact]
    public async Task Run_whose_strategy_is_gone_is_rejected()
    {
        var (id, trade) = await SaveRunAsync();
        _instances.Instances.Clear();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService().GetAsync(id, trade.EntryTimeUtc, trade.ExitTimeUtc, CancellationToken.None));
    }
}
