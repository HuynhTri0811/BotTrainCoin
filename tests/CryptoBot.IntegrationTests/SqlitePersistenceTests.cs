using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.Futures;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;
using CryptoBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CryptoBot.IntegrationTests;

/// <summary>Chạy trên file SQLite thật với migration thật.</summary>
public sealed class SqlitePersistenceTests : IAsyncLifetime
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly CandleKey Key = new("Binance", "BTCUSDT", CandleInterval.H1);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"cryptobot-test-{Guid.NewGuid():N}.db");

    private BotDbContext NewContext() =>
        new(new DbContextOptionsBuilder<BotDbContext>().UseSqlite($"Data Source={_path};Pooling=False").Options);

    public async Task InitializeAsync()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        foreach (var f in new[] { _path, _path + "-wal", _path + "-shm" })
            File.Delete(f);
        return Task.CompletedTask;
    }

    private static Candle MakeCandle(int hour, decimal close, bool closed = true) =>
        new("Binance", "BTCUSDT", CandleInterval.H1, T0 + TimeSpan.FromHours(hour),
            close, close, close, close, 0.00000001m, closed, T0);

    [Fact]
    public async Task Decimal_and_utc_timestamps_round_trip_exactly()
    {
        await using (var db = NewContext())
        {
            db.Candles.Add(new Candle("Binance", "BTCUSDT", CandleInterval.H1, T0,
                open: 123456789.12345678m, high: 123456789.12345679m, low: 0.00000001m, close: 79228162514264.337593543950335m,
                volume: 0.1m, isClosed: true, receivedAtUtc: T0.AddMilliseconds(123)));
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var c = await db.Candles.SingleAsync();
            Assert.Equal(123456789.12345678m, c.Open);
            Assert.Equal(0.00000001m, c.Low);
            Assert.Equal(79228162514264.337593543950335m, c.Close);
            Assert.Equal(DateTimeKind.Utc, c.OpenTimeUtc.Kind);
            Assert.Equal(T0, c.OpenTimeUtc);
            Assert.Equal(T0.AddMilliseconds(123), c.ReceivedAtUtc);
        }
    }

    [Fact]
    public async Task Candle_natural_key_is_unique()
    {
        await using var db = NewContext();
        db.Candles.Add(MakeCandle(0, 10));
        db.Candles.Add(MakeCandle(0, 11));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Upsert_updates_open_candle_and_keeps_closed_candle()
    {
        await using (var db = NewContext())
            await new CandleStore(db).UpsertAsync(Key, [MakeCandle(0, 10), MakeCandle(1, 20, closed: false)], default);

        UpsertResult result;
        await using (var db = NewContext())
            result = await new CandleStore(db).UpsertAsync(Key,
                [MakeCandle(0, 99), MakeCandle(1, 21), MakeCandle(2, 30, closed: false)], default);

        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Updated);
        Assert.Single(result.Conflicts);

        await using (var db = NewContext())
        {
            var all = await new CandleStore(db).GetLatestAsync(Key, null, 10, closedOnly: false, default);
            Assert.Equal([10m, 21m, 30m], all.Select(c => c.Close));
            Assert.Equal([true, true, false], all.Select(c => c.IsClosed));

            var closed = await new CandleStore(db).GetAfterAsync(Key, T0, 10, closedOnly: true, default);
            Assert.Equal(T0 + TimeSpan.FromHours(1), Assert.Single(closed).OpenTimeUtc);
        }
    }

    [Fact]
    public async Task Time_ordering_works_in_sql()
    {
        await using (var db = NewContext())
            await new CandleStore(db).UpsertAsync(Key, Enumerable.Range(0, 30).Select(h => MakeCandle(h, h + 1)).ToList(), default);

        await using (var db = NewContext())
        {
            var store = new CandleStore(db);
            Assert.Equal(T0 + TimeSpan.FromHours(29), await store.GetLatestOpenTimeAsync(Key, default));
            var before = await store.GetLatestAsync(Key, T0 + TimeSpan.FromHours(10), 3, closedOnly: true, default);
            Assert.Equal([8m, 9m, 10m], before.Select(c => c.Close));
        }
    }

    [Fact]
    public async Task Duplicate_signal_rolls_back_checkpoint()
    {
        int instanceId;
        await using (var db = NewContext())
        {
            var instance = new StrategyInstance("s", "EmaCross", "{}", "Binance", "BTCUSDT", "1h", true);
            db.StrategyInstances.Add(instance);
            await db.SaveChangesAsync();
            instanceId = instance.Id;

            instance.MarkEvaluated(T0);
            await new SignalStore(db).SaveEvaluationAsync(instance, [Signal(instanceId)], default);
        }

        await using (var db = NewContext())
        {
            var instance = await db.StrategyInstances.SingleAsync();
            instance.MarkEvaluated(T0 + TimeSpan.FromHours(1));

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                new SignalStore(db).SaveEvaluationAsync(instance, [Signal(instanceId)], default));
        }

        await using (var db = NewContext())
        {
            Assert.Equal(T0, (await db.StrategyInstances.SingleAsync()).LastEvaluatedCandleOpenTimeUtc);
            Assert.Equal(1, await db.Signals.CountAsync());
            var stored = await new SignalStore(db).GetLastAsync(instanceId, 1, default);
            Assert.Equal(SignalAction.Buy, stored!.Action);
            Assert.Equal(100.5m, stored.CandleClose);
        }
    }

    [Fact]
    public async Task Indicator_auto_rule_round_trips_with_nullable_price_fields()
    {
        await using (var db = NewContext())
        {
            db.AutoOrderRules.Add(AutoOrderRule.ForIndicator("BTCUSDT", PositionSide.Short, "btc-15m-myrsi",
                50m, 2, 1m, 2m, T0));
            db.AutoOrderRules.Add(new AutoOrderRule("BTCUSDT", PositionSide.Long, TriggerCondition.AtOrBelow,
                98_000.5m, 50m, 2, 1m, null, T0));
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var rules = await db.AutoOrderRules.OrderBy(r => r.Id).ToListAsync();
            Assert.Equal(RuleTriggerType.Indicator, rules[0].TriggerType);
            Assert.Equal("btc-15m-myrsi", rules[0].IndicatorName);
            Assert.Null(rules[0].TriggerPrice);
            Assert.Null(rules[0].Condition);
            Assert.Equal(RuleTriggerType.Price, rules[1].TriggerType);
            Assert.Equal(98_000.5m, rules[1].TriggerPrice);
        }
    }

    [Fact]
    public async Task Watching_close_query_returns_only_triggered_rules_still_watching()
    {
        await using (var db = NewContext())
        {
            var watching = AutoOrderRule.ForIndicator("BTCUSDT", PositionSide.Long, "rsi", 50m, 2, 1m, 2m, T0,
                closeOnReverse: true);
            watching.MarkTriggered(100m, 1, T0);
            var ended = AutoOrderRule.ForIndicator("BTCUSDT", PositionSide.Long, "rsi", 50m, 2, 1m, 2m, T0,
                closeOnReverse: true);
            ended.MarkTriggered(100m, 2, T0);
            ended.EndCloseWatch("Vị thế đã kết thúc trước (StopLoss).", T0);
            var pending = AutoOrderRule.ForIndicator("BTCUSDT", PositionSide.Long, "rsi", 50m, 2, 1m, 2m, T0,
                closeOnReverse: true);
            var noClose = AutoOrderRule.ForIndicator("BTCUSDT", PositionSide.Long, "rsi", 50m, 2, 1m, 2m, T0);
            noClose.MarkTriggered(100m, 3, T0);
            db.AutoOrderRules.AddRange(watching, ended, pending, noClose);
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var rule = Assert.Single(await new AutoOrderStore(db).GetWatchingCloseAsync(default));
            Assert.Equal(1, rule.PositionId);
            Assert.NotNull(rule.CloseCommandId);
            Assert.True(rule.IsWatchingClose);
        }
    }

    [Fact]
    public async Task Daily_report_is_unique_per_symbol_and_period_end_and_listed_newest_first()
    {
        DailyReport Report(string symbol, int day) =>
            new(symbol, "1h", T0.AddDays(day - 1), T0.AddDays(day), T0.AddDays(day), "{}");

        await using (var db = NewContext())
        {
            db.DailyReports.AddRange(Report("BTCUSDT", 1), Report("BTCUSDT", 2), Report("ETHUSDT", 2));
            await db.SaveChangesAsync();
        }

        await using (var db = NewContext())
        {
            var store = new DailyReportStore(db);
            Assert.True(await store.ExistsAsync("BTCUSDT", T0.AddDays(2), default));
            Assert.False(await store.ExistsAsync("BTCUSDT", T0.AddDays(3), default));
            Assert.Equal([T0.AddDays(2), T0.AddDays(1)],
                (await store.GetRecentAsync("BTCUSDT", 10, default)).Select(r => r.PeriodEndUtc));
            Assert.Equal(3, (await store.GetRecentAsync(null, 10, default)).Count);

            db.DailyReports.Add(Report("BTCUSDT", 2));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    private static TradingSignal Signal(int instanceId) =>
        new(instanceId, 1, "BTCUSDT", "1h", T0, SignalAction.Buy, "test", 100.5m, 101m, 100m, T0, isBackfill: true);
}
