using CryptoBot.Application.Indicators;
using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.Application.Tests;

public class IndicatorRegistryTests
{
    private static Dictionary<string, string?> P(params (string Key, string Value)[] items) =>
        items.ToDictionary(x => x.Key, x => (string?)x.Value, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Finds_indicator_classes_by_attribute_only()
    {
        Assert.True(IndicatorRegistry.AvailableTypes.ContainsKey("MyRsi"));
        // Khung trống không có [Indicator] nên không được đăng ký.
        Assert.DoesNotContain(typeof(TemplateIndicator), IndicatorRegistry.AvailableTypes.Values);
    }

    [Fact]
    public void Binds_parameters_case_insensitively_with_invariant_decimals()
    {
        var ind = Assert.IsType<MyRsiIndicator>(IndicatorRegistry.Create("myrsi",
            P(("period", "7"), ("Oversold", "25.5"), ("LookbackCandles", "50"))));

        Assert.Equal(7, ind.Period);
        Assert.Equal(25.5m, ind.Oversold);
        Assert.Equal(70m, ind.Overbought);   // mặc định giữ nguyên
        Assert.Equal(50, ind.RequiredCandles);
    }

    [Theory]
    [InlineData("Nope", "Period", "14", "Không có chỉ báo loại")]
    [InlineData("MyRsi", "Peroid", "14", "không tồn tại")]
    [InlineData("MyRsi", "Period", "abc", "không đúng kiểu")]
    [InlineData("MyRsi", "Oversold", "80", "Oversold < Overbought")]
    public void Rejects_bad_configuration(string type, string key, string value, string expected)
    {
        var ex = Assert.Throws<ArgumentException>(() => IndicatorRegistry.Create(type, P((key, value))));
        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Bad_definitions_are_skipped_with_errors_and_good_ones_kept()
    {
        var registry = new IndicatorRegistry(
        [
            new IndicatorDefinition { Name = "ok", Type = "MyRsi", Symbol = "btcusdt", Interval = "15m" },
            new IndicatorDefinition { Name = "ok", Type = "MyRsi", Symbol = "BTCUSDT", Interval = "15m" },
            new IndicatorDefinition { Name = "bad-interval", Type = "MyRsi", Symbol = "BTCUSDT", Interval = "3m" },
            new IndicatorDefinition { Name = "bad-param", Type = "MyRsi", Symbol = "BTCUSDT", Interval = "1h", Parameters = P(("X", "1")) },
            new IndicatorDefinition { Name = "off", Type = "Nope", Symbol = "BTCUSDT", Enabled = false },
        ], NullLogger<IndicatorRegistry>.Instance);

        var ok = Assert.Single(registry.All);
        Assert.Equal("BTCUSDT", ok.Key.Symbol);
        Assert.Equal(CandleInterval.M15, ok.Key.Interval);
        Assert.Same(ok, registry.Find("OK"));
        Assert.Equal(["ok", "bad-interval", "bad-param"], registry.Errors.Select(e => e.Name));
    }
}
