using System.IO.Compression;
using System.Text.Json.Nodes;
using CryptoBot.Application.Backtesting;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.Futures;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Tests;

public class BacktestHistoryServiceTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly BacktestSettings Settings = new(1000m, PositionSizing.PercentOfEquity, 10m, 5m, 3m, 0.1m);

    // Hai vòng BUY/SELL với EMA 2/3 trên cửa sổ 4 nến.
    private static readonly decimal[] TwoRounds = [10, 10, 10, 13, 14, 15, 16, 8, 7, 7, 7, 12, 13, 14, 6, 6];

    private readonly InMemoryCandleStore _candles = new();
    private readonly InMemoryStrategyInstanceStore _instances = new();
    private readonly InMemoryBacktestStore _store = new();
    private readonly FakeClock _clock = new(T0.AddDays(30));

    public BacktestHistoryServiceTests()
    {
        var instance = new StrategyInstance("test", StrategyTypes.EmaCross,
            StrategyFactory.Serialize(new EmaCrossParameters(2, 3, 4)), "Binance", "BTCUSDT", "1h", true);
        typeof(StrategyInstance).GetProperty(nameof(StrategyInstance.Id))!.SetValue(instance, 1);
        _instances.Instances.Add(instance);
        for (var index = 0; index < TwoRounds.Length; index++)
        {
            var close = TwoRounds[index];
            var open = T0 + TimeSpan.FromHours(index);
            _candles.Candles[open] = new Candle("Binance", "BTCUSDT", CandleInterval.H1, open, close, close, close,
                close, 1, true, T0);
        }
    }

    private BacktestHistoryService CreateService() => new(_store, _clock);

    private async Task<BacktestRun> RunAsync() =>
        (await new BacktestService(_instances, _candles).RunAsync(
            new BacktestRequest(1, null, null, null, Settings), CancellationToken.None))!;

    [Fact]
    public async Task Saved_run_reopens_with_the_same_result()
    {
        var run = await RunAsync();
        var service = CreateService();

        var saved = await service.SaveAsync(run, CancellationToken.None);
        var reopened = await service.GetAsync(saved.Saved.Id, CancellationToken.None);

        Assert.NotNull(reopened);
        var snapshot = reopened.Snapshot;
        Assert.Equal(run.Result.Statistics, snapshot.Statistics);
        Assert.Equal(run.Result.Trades, snapshot.Trades);
        Assert.Equal(Settings, snapshot.Settings);
        Assert.Equal(run.Parameters, snapshot.Parameters);
        Assert.Equal(2, snapshot.BuySignals);
        Assert.Equal(2, snapshot.SellSignals);
        Assert.Equal(run.Chart.Bars, snapshot.Chart.Bars);
        Assert.Equal(run.Chart.Bars, snapshot.Chart.Bars);
        Assert.Equal(_clock.UtcNow, reopened.Saved.CreatedAtUtc);
        Assert.Equal(run.Result.Statistics.NetProfit, reopened.Saved.NetProfit);
    }

    [Fact]
    public async Task Enums_are_stored_by_name()
    {
        var saved = await CreateService().SaveAsync(await RunAsync(), CancellationToken.None);

        await using var gzip = new GZipStream(new MemoryStream(saved.Saved.Content), CompressionMode.Decompress);
        var json = await new StreamReader(gzip).ReadToEndAsync();

        Assert.Contains("\"exitReason\":\"TakeProfit\"", json);
        Assert.Contains("\"sizing\":\"PercentOfEquity\"", json);
    }

    [Fact]
    public async Task Runs_saved_before_futures_reopen_as_long_ema_cross_spot_runs()
    {
        var saved = await CreateService().SaveAsync(await RunAsync(), CancellationToken.None);
        // Bỏ các trường mới để giống nội dung lưu trước khi có Futures và nguồn tín hiệu.
        var node = JsonNode.Parse(await ReadJsonAsync(saved.Saved.Content))!.AsObject();
        node.Remove("signal");
        foreach (var trade in node["trades"]!.AsArray())
            trade!.AsObject().Remove("side");
        node["settings"]!.AsObject().Remove("futures");
        var old = new SavedBacktest(T0, 1, "BTCUSDT", "1h", "EmaCross", null, 2, 3, 4, null, null, Settings,
            saved.Snapshot.Statistics, Gzip(node.ToJsonString()));
        _store.Add(old);

        var snapshot = (await CreateService().GetAsync(old.Id, CancellationToken.None))!.Snapshot;

        Assert.Null(snapshot.Signal);
        Assert.Equal(BacktestSignalSpec.ForEmaCross(new EmaCrossParameters(2, 3, 4)).Type,
            snapshot.SignalOrEmaCross.Type);
        Assert.Equal(4, snapshot.SignalOrEmaCross.RequiredCandles);
        Assert.All(snapshot.Trades, trade => Assert.Equal(PositionSide.Long, trade.Side));
        Assert.Null(snapshot.Settings.Futures);
    }

    [Fact]
    public async Task Indicator_futures_run_keeps_its_signal_and_settings()
    {
        var futures = Settings with
        {
            Futures = new FuturesBacktestSettings(3, BacktestDirection.Both, true, 0.05m, 0.4m),
        };
        var run = (await new BacktestService(_instances, _candles).RunAsync(new BacktestRequest(1, null, null, null,
            futures, "MyRsi", new Dictionary<string, string?> { ["Period"] = "2", ["LookbackCandles"] = "4" }),
            CancellationToken.None))!;

        var saved = await CreateService().SaveAsync(run, CancellationToken.None);
        var summary = (await CreateService().GetSummaryAsync(saved.Saved.Id, CancellationToken.None))!;
        var snapshot = (await CreateService().GetAsync(saved.Saved.Id, CancellationToken.None))!.Snapshot;

        Assert.Equal(("MyRsi", (int?)null, (int?)null, 4), (summary.SignalType, summary.FastPeriod,
            summary.SlowPeriod, summary.LookbackCandles));
        Assert.Equal("2", BacktestSignalSpec.ParseParameters(summary.SignalParameters)["Period"]);
        Assert.Equal(futures, summary.Settings);
        Assert.Equal(run.Signal.Parameters, snapshot.Signal!.Parameters);
        Assert.Null(snapshot.Parameters);
    }

    private static async Task<string> ReadJsonAsync(byte[] content)
    {
        await using var gzip = new GZipStream(new MemoryStream(content), CompressionMode.Decompress);
        return await new StreamReader(gzip).ReadToEndAsync();
    }

    private static byte[] Gzip(string json)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest))
            gzip.Write(System.Text.Encoding.UTF8.GetBytes(json));
        return buffer.ToArray();
    }

    [Fact]
    public async Task List_is_newest_first_and_filters_by_strategy()
    {
        var run = await RunAsync();
        var service = CreateService();
        await service.SaveAsync(run, CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        await service.SaveAsync(run, CancellationToken.None);

        var all = await service.ListAsync(null, 50, 0, CancellationToken.None);
        var other = await service.ListAsync(2, 50, 0, CancellationToken.None);

        Assert.Equal([2L, 1L], all.Select(summary => summary.Id));
        Assert.Equal(Settings, all[0].Settings);
        Assert.Empty(other);
    }

    [Fact]
    public async Task Note_can_be_set_and_run_deleted()
    {
        var service = CreateService();
        var saved = await service.SaveAsync(await RunAsync(), CancellationToken.None);

        var noted = await service.SetNoteAsync(saved.Saved.Id, " EMA mặc định ", CancellationToken.None);
        Assert.Equal("EMA mặc định", noted!.Note);

        Assert.True(await service.DeleteAsync(saved.Saved.Id, CancellationToken.None));
        Assert.Null(await service.GetAsync(saved.Saved.Id, CancellationToken.None));
        Assert.False(await service.DeleteAsync(saved.Saved.Id, CancellationToken.None));
        Assert.Null(await service.SetNoteAsync(99, "x", CancellationToken.None));
    }
}
