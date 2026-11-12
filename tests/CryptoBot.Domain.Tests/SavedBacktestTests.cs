using CryptoBot.Domain.Backtesting;

namespace CryptoBot.Domain.Tests;

public class SavedBacktestTests
{
    private static readonly BacktestSettings Settings =
        new(1000m, PositionSizing.PercentOfEquity, 10m, 10m, 3m, 0.1m);

    private static readonly BacktestStatistics Statistics =
        new(4, 1, 3, 25m, -12.5m, -1.25m, 10m, 22.5m, 0.44m, 0.8m, 30m, 3m, 987.5m, 5m);

    private static SavedBacktest Create(byte[]? content = null) =>
        new(TestCandles.T0, 1, "BTCUSDT", "1h", "EmaCross", null, 9, 21, 300, TestCandles.T0,
            TestCandles.T0.AddDays(1), Settings, Statistics, content ?? [1, 2, 3]);

    [Fact]
    public void Copies_settings_and_headline_statistics()
    {
        SavedBacktest saved = Create();

        Assert.Equal(Settings, saved.Settings);
        Assert.Equal(4, saved.TotalTrades);
        Assert.Equal(25m, saved.WinRatePercent);
        Assert.Equal(-12.5m, saved.NetProfit);
        Assert.Equal(3m, saved.MaxDrawdownPercent);
        Assert.Null(saved.Note);
    }

    [Fact]
    public void Note_is_trimmed_and_blank_clears_it()
    {
        SavedBacktest saved = Create();

        saved.SetNote("  phí maker  ");
        Assert.Equal("phí maker", saved.Note);

        saved.SetNote("   ");
        Assert.Null(saved.Note);
    }

    [Fact]
    public void Too_long_note_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Create().SetNote(new string('x', SavedBacktest.MaxNoteLength + 1)));
    }

    [Fact]
    public void Empty_content_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Create([]));
    }

    [Fact]
    public void Keeps_the_indicator_signal_and_rebuilds_the_futures_settings()
    {
        BacktestSettings futures = Settings with
        {
            Futures = new FuturesBacktestSettings(5, BacktestDirection.ShortOnly, false, 0.05m, 0.4m),
        };

        SavedBacktest saved = new(TestCandles.T0, 1, "BTCUSDT", "1h", "MyRsi", "{\"Period\":\"14\"}", null, null,
            200, null, null, futures, Statistics, [1]);

        Assert.Equal(("MyRsi", "{\"Period\":\"14\"}"), (saved.SignalType, saved.SignalParameters));
        Assert.Null(saved.FastPeriod);
        Assert.Equal(futures, saved.Settings);
        Assert.Equal(BacktestMode.Spot, Create().Settings.Mode);
    }

    [Fact]
    public void Rejects_a_missing_signal_type()
    {
        Assert.Throws<ArgumentException>(() => new SavedBacktest(TestCandles.T0, 1, "BTCUSDT", "1h", " ", null, 9,
            21, 300, null, null, Settings, Statistics, [1]));
    }
}
