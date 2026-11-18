using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Tests;

public class LongFlushIndicatorTests
{
    // ATR 2 nến, khối lượng TB 2 nến, chờ xác nhận 3 nến → cần ít nhất max(2 + 1, 2) + 3 + 1 = 7 nến.
    private static LongFlushIndicator Small(int lookback = 7) => new()
    {
        AtrPeriod = 2, DropAtr = 2m, VolumePeriod = 2, VolumeMultiplier = 2m, ConfirmCandles = 3,
        LookbackCandles = lookback,
    };

    // Nến yên: biên độ 2 (ATR = 2), khối lượng 10.
    private static (decimal Open, decimal High, decimal Low, decimal Close, decimal Volume) Calm =>
        (100m, 101m, 99m, 100m, 10m);

    // Nến xả: đâm từ 100 xuống 94 = 3 ATR, khối lượng 30 = 3× TB. Mức hồi = (100 + 94) / 2 = 97.
    private static (decimal Open, decimal High, decimal Low, decimal Close, decimal Volume) Flush =>
        (100m, 100m, 94m, 95m, 30m);

    // Nến xác nhận: xanh, đóng 98 > 97, không thủng đáy 94.
    private static (decimal Open, decimal High, decimal Low, decimal Close, decimal Volume) Confirm =>
        (95m, 98.5m, 94.5m, 98m, 12m);

    // Nến chờ: xanh nhưng đóng dưới mức hồi 97, không thủng đáy.
    private static (decimal Open, decimal High, decimal Low, decimal Close, decimal Volume) Waiting =>
        (95m, 96m, 94.5m, 95.5m, 12m);

    private static List<Candle> Build(
        params (decimal Open, decimal High, decimal Low, decimal Close, decimal Volume)[] bars) =>
        bars.Select((bar, index) => new Candle("Binance", "BTCUSDT", CandleInterval.H1,
            TestCandles.T0 + TimeSpan.FromHours(index), bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true,
            TestCandles.T0)).ToList();

    [Fact]
    public void First_green_close_above_the_flush_midpoint_is_long()
    {
        IndicatorResult result = Small().Compute(Build(Calm, Calm, Calm, Calm, Calm, Flush, Confirm));

        Assert.Equal(IndicatorSignal.Long, result.Signal);
        Assert.Equal(97m, result.Values["reclaim"]);
        Assert.Contains("Xả Long lúc 2026-01-01 05:00 UTC", result.Reason);
        Assert.Contains("đâm 3 ATR, khối lượng 3× TB", result.Reason);
    }

    [Fact]
    public void Only_the_first_confirmation_signals()
    {
        IndicatorResult result = Small().Compute(Build(Calm, Calm, Calm, Calm, Flush, Confirm, Confirm));

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Null(result.Values["reclaim"]);
    }

    [Fact]
    public void Waiting_after_a_flush_shows_the_reclaim_level()
    {
        IndicatorResult result = Small().Compute(Build(Calm, Calm, Calm, Calm, Calm, Flush, Waiting));

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Equal(97m, result.Values["reclaim"]);
        Assert.Contains("chờ nến xanh đóng trên 97", result.Reason);
    }

}
