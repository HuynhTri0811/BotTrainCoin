using CryptoBot.Application.Signals;
using CryptoBot.Domain.Strategies;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.Application.Tests;

public class StrategyConfiguratorTests
{
    private readonly InMemoryStrategyInstanceStore _store = new();

    private StrategyConfigurator CreateConfigurator() => new(_store, NullLogger<StrategyConfigurator>.Instance);

    private static StrategyDefinition Definition(string name = "btc-1h-ema", string symbol = "BTCUSDT",
        string interval = "1h", EmaCrossParameters? parameters = null, bool enabled = true) =>
        new()
        {
            Name = name,
            Symbol = symbol,
            Interval = interval,
            Enabled = enabled,
            Parameters = parameters ?? new EmaCrossParameters(),
        };

    [Fact]
    public async Task Creates_instance_for_new_definition_and_saves_once()
    {
        await CreateConfigurator().ApplyAsync([Definition(), Definition("eth-15m-ema", "ETHUSDT", "15m")], default);

        Assert.Equal(["btc-1h-ema", "eth-15m-ema"], _store.Instances.Select(i => i.Name));
        var btc = _store.Instances[0];
        Assert.Equal(StrategyTypes.EmaCross, btc.StrategyType);
        Assert.Equal("Binance", btc.Exchange);
        Assert.Equal(1, btc.Version);
        Assert.Equal(1, _store.Saves);
    }

    [Fact]
    public async Task Applying_same_definition_again_keeps_version()
    {
        var configurator = CreateConfigurator();
        await configurator.ApplyAsync([Definition()], default);

        await configurator.ApplyAsync([Definition()], default);

        Assert.Equal(1, Assert.Single(_store.Instances).Version);
    }

    [Fact]
    public async Task Changing_parameters_bumps_version_of_existing_instance()
    {
        var configurator = CreateConfigurator();
        await configurator.ApplyAsync([Definition()], default);

        await configurator.ApplyAsync([Definition(parameters: new EmaCrossParameters(FastPeriod: 12))], default);

        var instance = Assert.Single(_store.Instances);
        Assert.Equal(2, instance.Version);
        Assert.Equal(12, StrategyFactory.ParseEmaCross(instance.ParametersJson).FastPeriod);
    }

    [Fact]
    public async Task Disabling_keeps_version()
    {
        var configurator = CreateConfigurator();
        await configurator.ApplyAsync([Definition()], default);

        await configurator.ApplyAsync([Definition(enabled: false)], default);

        var instance = Assert.Single(_store.Instances);
        Assert.False(instance.Enabled);
        Assert.Equal(1, instance.Version);
    }

    [Theory]
    [InlineData("", "BTCUSDT", "1h")]
    [InlineData("bad-symbol", "BTC/USDT", "1h")]
    [InlineData("bad-interval", "BTCUSDT", "4h")]
    public async Task Invalid_definitions_are_skipped_without_stopping_the_others(string name, string symbol,
        string interval)
    {
        await CreateConfigurator().ApplyAsync([Definition(name, symbol, interval), Definition("ok")], default);

        Assert.Equal("ok", Assert.Single(_store.Instances).Name);
    }

    [Fact]
    public async Task Unknown_type_and_invalid_parameters_are_skipped()
    {
        var unknownType = Definition("unknown");
        unknownType.Type = "Macd";
        var badParams = Definition("bad-params", parameters: new EmaCrossParameters(FastPeriod: 21, SlowPeriod: 9));

        await CreateConfigurator().ApplyAsync([unknownType, badParams], default);

        Assert.Empty(_store.Instances);
    }

    [Fact]
    public void Factory_round_trips_parameters_and_rejects_unknown_type()
    {
        var parameters = new EmaCrossParameters(5, 13, 100);
        var json = StrategyFactory.Serialize(parameters);

        Assert.Equal(parameters, StrategyFactory.ParseEmaCross(json));
        Assert.Equal(100, StrategyFactory.Create(new StrategyInstance("x", StrategyTypes.EmaCross, json, "Binance",
            "BTCUSDT", "1h", true)).RequiredCandles);
        Assert.Throws<NotSupportedException>(() => StrategyFactory.Create(new StrategyInstance("x", "Macd", json,
            "Binance", "BTCUSDT", "1h", true)));
    }
}
