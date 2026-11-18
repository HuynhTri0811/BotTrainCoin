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

    [Fact]
    public void A_new_low_below_the_flush_cancels_it()
    {
        IndicatorResult result = Small().Compute(Build(Calm, Calm, Calm, Calm, Flush,
            (95m, 95.5m, 93m, 94m, 12m), (94m, 99m, 93.5m, 99m, 12m)));

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Null(result.Values["reclaim"]);
    }

    [Fact]
    public void A_drop_without_a_volume_spike_is_not_a_flush()
    {
        IndicatorResult result = Small().Compute(Build(Calm, Calm, Calm, Calm, Calm,
            (100m, 100m, 94m, 95m, 12m), Confirm));

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Contains("Chưa có cú xả", result.Reason);
    }

    [Fact]
    public void A_small_drop_with_high_volume_is_not_a_flush()
    {
        IndicatorResult result = Small().Compute(Build(Calm, Calm, Calm, Calm, Calm,
            (100m, 100m, 97m, 98m, 30m), (98m, 99.5m, 97.5m, 99m, 12m)));

        Assert.Equal(IndicatorSignal.None, result.Signal);
    }

    [Fact]
    public void Confirmation_after_the_waiting_limit_is_ignored()
    {
        // Xả ở nến 5, xác nhận ở nến 9: cách 4 nến, quá ConfirmCandles = 3.
        IndicatorResult result = Small(lookback: 10).Compute(Build(Calm, Calm, Calm, Calm, Calm, Flush,
            Waiting, Waiting, Waiting, Confirm));

        Assert.Equal(IndicatorSignal.None, result.Signal);
    }

    [Fact]
    public void Uses_only_the_last_lookback_candles()
    {
        IndicatorResult result = Small().Compute(Build((500m, 900m, 1m, 2m, 9999m), (2m, 3m, 1m, 2m, 1m),
            Calm, Calm, Calm, Calm, Calm, Flush, Confirm));

        Assert.Equal(IndicatorSignal.Long, result.Signal);
        Assert.Equal(97m, result.Values["reclaim"]);
    }

    [Fact]
    public void Warm_up_without_enough_candles()
    {
        IndicatorResult result = Small().Compute(Build(Calm, Calm, Flush, Confirm));

        Assert.Equal(IndicatorSignal.None, result.Signal);
        Assert.Contains("Warm-up", result.Reason);
        Assert.Empty(result.Values);
    }

    [Fact]
    public void Never_signals_short_even_after_a_spike_up()
    {
        LongFlushIndicator indicator = Small();
        // Cú bơm lên đối xứng với cú xả, rồi giá rơi.
        List<Candle> candles = Build(Calm, Calm, Calm, Calm, Calm, (100m, 106m, 100m, 105m, 30m),
            (105m, 105.5m, 102m, 102.5m, 12m), (102.5m, 103m, 95m, 95.5m, 30m), Confirm);

        List<IndicatorSignal> signals = Enumerable.Range(indicator.LookbackCandles, candles.Count - 6)
            .Select(count => indicator.Compute(candles.GetRange(0, count)).Signal)
            .ToList();

        Assert.DoesNotContain(IndicatorSignal.Short, signals);
    }

    [Fact]
    public void The_reclaim_shown_on_a_signal_belongs_to_the_flush_that_fired()
    {
        // Xả f1 ở nến 5 (mức hồi 97), rồi xả nông hơn f2 ở nến 6 (đáy 96, mức hồi 100). Nến cuối đóng 98:
        // xác nhận f1 nhưng chưa xác nhận f2 → Long theo f1, chart phải vẽ mức hồi 97 chứ không phải 100.
        IndicatorResult result = Small(lookback: 8).Compute(Build(Calm, Calm, Calm, Calm, Calm, Flush,
            (104m, 104m, 96m, 97m, 40m), (97m, 98.5m, 96.5m, 98m, 12m)));

        Assert.Equal(IndicatorSignal.Long, result.Signal);
        Assert.Equal(97m, result.Values["reclaim"]);
        Assert.Contains("05:00 UTC", result.Reason);
        Assert.Contains("đóng trên 97", result.Reason);
    }

    [Fact]
    public void Lines_match_the_value_keys()
    {
        LongFlushIndicator indicator = Small();

        IndicatorResult result = indicator.Compute(Build(Calm, Calm, Calm, Calm, Calm, Flush, Confirm));

        Assert.Equal(indicator.Lines.Select(line => line.Key).Order(), result.Values.Keys.Order());
    }

    [Fact]
    public void Default_parameters_are_valid()
    {
        LongFlushIndicator indicator = new();

        Assert.Null(indicator.Validate());
        Assert.Equal((14, 2m, 20, 2m, 6, 100), (indicator.AtrPeriod, indicator.DropAtr, indicator.VolumePeriod,
            indicator.VolumeMultiplier, indicator.ConfirmCandles, indicator.RequiredCandles));
    }

    [Theory]
    [InlineData(0, 2, 2, 2, 3, 7)]   // AtrPeriod < 1
    [InlineData(2, 0, 2, 2, 3, 7)]   // DropAtr <= 0
    [InlineData(2, 2, 0, 2, 3, 7)]   // VolumePeriod < 1
    [InlineData(2, 2, 2, 0, 3, 7)]   // VolumeMultiplier <= 0
    [InlineData(2, 2, 2, 2, 0, 7)]   // ConfirmCandles < 1
    [InlineData(2, 2, 2, 2, 3, 6)]   // LookbackCandles < max(AtrPeriod + 1, VolumePeriod) + ConfirmCandles + 1
    public void Validate_rejects_bad_parameters(int atr, int drop, int volumePeriod, int volumeMultiplier, int confirm,
        int lookback)
    {
        LongFlushIndicator indicator = new()
        {
            AtrPeriod = atr, DropAtr = drop, VolumePeriod = volumePeriod, VolumeMultiplier = volumeMultiplier,
            ConfirmCandles = confirm, LookbackCandles = lookback,
        };

        Assert.NotNull(indicator.Validate());
    }
}
