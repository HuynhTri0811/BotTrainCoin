using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

public class CandleIntervalTests
{
    [Theory]
    [InlineData("15m", 15)]
    [InlineData("1h", 60)]
    [InlineData("1d", 1440)]
    public void Parses_supported_codes_with_their_duration(string code, int minutes)
    {
        Assert.True(CandleInterval.TryParse(code, out var interval));
        Assert.Equal(code, interval.Code);
        Assert.Equal(TimeSpan.FromMinutes(minutes), interval.Duration);
        Assert.Equal(code, interval.ToString());
        Assert.Same(interval, CandleInterval.Parse(code));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4h")]
    [InlineData("1H")] // Mã của Binance phân biệt hoa thường.
    public void Rejects_unsupported_codes(string? code)
    {
        Assert.False(CandleInterval.TryParse(code, out var interval));
        Assert.Null(interval);
    }

    [Fact]
    public void Parse_throws_with_the_bad_code_in_the_message()
    {
        var ex = Assert.Throws<ArgumentException>(() => CandleInterval.Parse("4h"));

        Assert.Contains("'4h'", ex.Message);
    }
}
