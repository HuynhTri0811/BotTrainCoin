using CryptoBot.Application.Signals;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.Application.Tests;

public class SignalEvaluationServiceTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string Params = StrategyFactory.Serialize(new EmaCrossParameters(2, 3, 4));

    private readonly InMemoryCandleStore _candles = new();
    private readonly InMemorySignalStore _signals = new();
    private readonly FakeClock _clock = new(T0);

    private SignalEvaluationService CreateService() =>
        new(_candles, _signals, _clock, new SignalOptions(), NullLogger<SignalEvaluationService>.Instance);

    private static StrategyInstance CreateInstance()
    {
        var instance = new StrategyInstance("test", StrategyTypes.EmaCross, Params, "Binance", "BTCUSDT", "1h", true);
        typeof(StrategyInstance).GetProperty(nameof(StrategyInstance.Id))!.SetValue(instance, 1);
        return instance;
    }

    private void AddCandles(params decimal[] closes)
    {
        var start = _candles.Candles.Count;
        for (var i = 0; i < closes.Length; i++)
        {
            var c = closes[i];
            var open = T0 + TimeSpan.FromHours(start + i);
            _candles.Candles[open] = new Candle("Binance", "BTCUSDT", CandleInterval.H1, open, c, c, c, c, 1, true, T0);
        }
    }

    // Lên ở nến 3, xuống ở nến 6.
    private static readonly decimal[] UpThenDown = [10, 10, 10, 13, 13, 13, 7];

    [Fact]
    public async Task Emits_buy_then_sell_and_advances_checkpoint()
    {
        AddCandles(UpThenDown);
        _clock.UtcNow = T0 + TimeSpan.FromHours(7) + TimeSpan.FromSeconds(30);
        var instance = CreateInstance();

        var result = await CreateService().EvaluatePendingAsync(instance, CancellationToken.None);

        Assert.Equal(7, result.Evaluated);
        Assert.Equal([SignalAction.Buy, SignalAction.Sell], _signals.Signals.Select(s => s.Action));
        Assert.Equal(T0 + TimeSpan.FromHours(3), _signals.Signals[0].CandleOpenTimeUtc);
        Assert.Equal(T0 + TimeSpan.FromHours(6), instance.LastEvaluatedCandleOpenTimeUtc);
    }

    [Fact]
    public async Task Each_closed_candle_is_evaluated_only_once()
    {
        AddCandles(UpThenDown);
        var instance = CreateInstance();
        var service = CreateService();

        await service.EvaluatePendingAsync(instance, CancellationToken.None);
        var second = await service.EvaluatePendingAsync(instance, CancellationToken.None);

        Assert.Equal(0, second.Evaluated);
        Assert.Equal(2, _signals.Signals.Count);
    }

    [Fact]
    public async Task Incremental_evaluation_matches_full_replay()
    {
        AddCandles(UpThenDown[..5]);
        var incremental = CreateInstance();
        var service = CreateService();
        await service.EvaluatePendingAsync(incremental, CancellationToken.None);
        AddCandles(UpThenDown[5..]);
        await service.EvaluatePendingAsync(incremental, CancellationToken.None);
        var incrementalSignals = _signals.Signals.Select(s => (s.Action, s.CandleOpenTimeUtc, s.FastEma, s.SlowEma)).ToList();

        _signals.Signals.Clear();
        await CreateService().EvaluatePendingAsync(CreateInstance(), CancellationToken.None);
        var replaySignals = _signals.Signals.Select(s => (s.Action, s.CandleOpenTimeUtc, s.FastEma, s.SlowEma)).ToList();

        Assert.Equal(replaySignals, incrementalSignals);
    }

    [Fact]
    public async Task Old_candles_are_marked_backfill_and_fresh_candle_is_not()
    {
        AddCandles(10, 10, 10, 13);
        // Nến 3 đóng lúc T0+4h; đánh giá 30 giây sau -> mới.
        _clock.UtcNow = T0 + TimeSpan.FromHours(4) + TimeSpan.FromSeconds(30);
        await CreateService().EvaluatePendingAsync(CreateInstance(), CancellationToken.None);
        Assert.False(Assert.Single(_signals.Signals).IsBackfill);

        _signals.Signals.Clear();
        _clock.UtcNow = T0 + TimeSpan.FromDays(1);
        await CreateService().EvaluatePendingAsync(CreateInstance(), CancellationToken.None);
        Assert.True(Assert.Single(_signals.Signals).IsBackfill);
    }

    [Fact]
    public async Task Window_with_missing_candle_produces_no_signal()
    {
        AddCandles(10, 10, 10, 13);
        // Bỏ nến thứ 2 -> cửa sổ 4 nến cuối có khoảng thiếu.
        _candles.Candles.Remove(T0 + TimeSpan.FromHours(1));
        _candles.Candles[T0 + TimeSpan.FromHours(4)] =
            new Candle("Binance", "BTCUSDT", CandleInterval.H1, T0 + TimeSpan.FromHours(4), 13, 13, 13, 13, 1, true, T0);

        await CreateService().EvaluatePendingAsync(CreateInstance(), CancellationToken.None);

        Assert.Empty(_signals.Signals);
    }

    [Fact]
    public async Task Unclosed_candle_is_not_evaluated()
    {
        AddCandles(10, 10, 10);
        var open = T0 + TimeSpan.FromHours(3);
        _candles.Candles[open] = new Candle("Binance", "BTCUSDT", CandleInterval.H1, open, 13, 13, 13, 13, 1, false, T0);
        var instance = CreateInstance();

        await CreateService().EvaluatePendingAsync(instance, CancellationToken.None);

        Assert.Empty(_signals.Signals);
        Assert.Equal(T0 + TimeSpan.FromHours(2), instance.LastEvaluatedCandleOpenTimeUtc);
    }

    [Fact]
    public async Task New_config_version_reevaluates_history_separately()
    {
        AddCandles(UpThenDown);
        var instance = CreateInstance();
        var service = CreateService();
        await service.EvaluatePendingAsync(instance, CancellationToken.None);

        instance.Configure(StrategyTypes.EmaCross, StrategyFactory.Serialize(new EmaCrossParameters(2, 3, 5)),
            "Binance", "BTCUSDT", "1h", true);
        await service.EvaluatePendingAsync(instance, CancellationToken.None);

        Assert.Equal(2, _signals.Signals.Count(s => s.ConfigVersion == 1));
        Assert.Equal(2, instance.Version);
        Assert.Equal(T0 + TimeSpan.FromHours(6), instance.LastEvaluatedCandleOpenTimeUtc);
    }
}
