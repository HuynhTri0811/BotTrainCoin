using CryptoBot.Application.Backtesting;
using CryptoBot.Application.Futures;
using CryptoBot.Application.Indicators;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.Strategies;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.Application.Tests;

public class BacktestOptionsServiceTests
{
    private readonly InMemoryStrategyInstanceStore _instances = new();

    public BacktestOptionsServiceTests()
    {
        AddInstance(1, "BTCUSDT", "1h");
        AddInstance(2, "SOLUSDT", "15m");
    }

    private void AddInstance(int id, string symbol, string interval)
    {
        var instance = new StrategyInstance($"test-{id}", StrategyTypes.EmaCross,
            StrategyFactory.Serialize(new EmaCrossParameters(9, 21, 300)), "Binance", symbol, interval, true);
        typeof(StrategyInstance).GetProperty(nameof(StrategyInstance.Id))!.SetValue(instance, id);
        _instances.Instances.Add(instance);
    }

    private BacktestOptionsService CreateService()
    {
        var registry = new IndicatorRegistry(
        [
            new IndicatorDefinition
            {
                Name = "btc-1h-myrsi", Type = "MyRsi", Symbol = "BTCUSDT", Interval = "1h",
                Parameters = new(StringComparer.OrdinalIgnoreCase) { ["Period"] = "21", ["Oversold"] = "25.5" },
            },
            // Cùng loại nhưng khác khung nến: không dùng làm gợi ý cho instance 1h.
            new IndicatorDefinition
            {
                Name = "btc-15m-flow", Type = "FlowRsi", Symbol = "BTCUSDT", Interval = "15m",
                Parameters = new(StringComparer.OrdinalIgnoreCase) { ["Period"] = "7" },
            },
        ], NullLogger<IndicatorRegistry>.Instance);
        var paper = new PaperFuturesOptions
        {
            TakerFeeRate = 0.0005m,
            SlippageRate = 0.0002m,
            Symbols = [new FuturesSymbolOptions { Symbol = "BTCUSDT", MaintenanceMarginRate = 0.005m }],
        };
        var autoOrders = new AutoOrderOptions
        {
            DefaultMargin = 40m, DefaultLeverage = 3, DefaultStopLossPercent = 1.5m, DefaultTakeProfitPercent = 4m,
        };
        return new BacktestOptionsService(_instances, registry, paper, autoOrders);
    }

    [Fact]
    public async Task Suggests_the_configured_indicator_on_the_same_pair_and_interval()
    {
        var options = (await CreateService().GetAsync(1, CancellationToken.None))!;

        var myRsi = Assert.Single(options.Indicators, choice => choice.Type == "MyRsi");
        Assert.Equal("btc-1h-myrsi", myRsi.PresetName);
        Assert.Equal(("21", "25.5"), (myRsi.Parameters["Period"], myRsi.Parameters["Oversold"]));
        Assert.NotEmpty(myRsi.Description);

        var flowRsi = Assert.Single(options.Indicators, choice => choice.Type == "FlowRsi");
        Assert.Null(flowRsi.PresetName);
        Assert.Equal("14", flowRsi.Parameters["Period"]);
    }

    [Fact]
    public async Task Futures_defaults_follow_paper_futures_and_auto_orders()
    {
        var btc = (await CreateService().GetAsync(1, CancellationToken.None))!.Futures;
        var sol = (await CreateService().GetAsync(2, CancellationToken.None))!.Futures;

        Assert.Equal(new BacktestFuturesDefaults(0.05m, 0.02m, 0.5m, 125, 40m, 3, 1.5m, 4m), btc);
        // Cặp chưa có trong Paper Futures: ký quỹ duy trì mặc định 0.4%.
        Assert.Equal(0.4m, sol.MaintenanceMarginPercent);
    }

    [Fact]
    public async Task Unknown_strategy_is_null()
    {
        Assert.Null(await CreateService().GetAsync(99, CancellationToken.None));
    }
}
