using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Indicators;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;
using Microsoft.Extensions.Logging.Abstractions;

namespace CryptoBot.Application.Tests;

/// <summary>
/// Khóa đầu ra của các hàm sinh tín hiệu (EMA cross, MyRsi) trên một chuỗi nến cố định.
/// Mọi tối ưu/tách hàm phải giữ đầu ra giống hệt từng chữ số. Cập nhật snapshot có chủ đích bằng
/// <c>UPDATE_GOLDEN=1 dotnet test</c>, rồi xem diff của file Golden/signal_outputs.txt.
/// </summary>
public class SignalGoldenTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly CandleKey Key = new("Binance", "BTCUSDT", CandleInterval.H1);

    private sealed class SingleInstanceStore(StrategyInstance instance) : IStrategyInstanceStore
    {
        public Task<IReadOnlyList<StrategyInstance>> GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<StrategyInstance>>([instance]);
        public Task<StrategyInstance?> GetAsync(int id, CancellationToken ct) => Task.FromResult<StrategyInstance?>(instance);
        public Task<StrategyInstance?> FindByNameAsync(string name, CancellationToken ct) => Task.FromResult<StrategyInstance?>(instance);
        public Task AddAsync(StrategyInstance strategyInstance, CancellationToken ct) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// 1500 nến 1h, random walk có seed cố định, đổi xu hướng mỗi 120 nến để có giao cắt EMA và RSI cực trị.
    /// Bỏ nến thứ 900 để đi qua nhánh "cửa sổ có khoảng thiếu".
    /// </summary>
    private static InMemoryCandleStore BuildCandles()
    {
        var store = new InMemoryCandleStore();
        var rnd = new Random(42);
        var price = 30_000m;
        for (var index = 0; index < 1500; index++)
        {
            var drift = (index / 120) % 2 == 0 ? 0.0015 : -0.0015;
            var change = (decimal)(drift + (rnd.NextDouble() - 0.5) * 0.02);
            var open = price;
            var close = Math.Round(open * (1 + change), 2);
            var high = Math.Max(open, close) + Math.Round((decimal)rnd.NextDouble() * 20m, 2);
            var low = Math.Min(open, close) - Math.Round((decimal)rnd.NextDouble() * 20m, 2);
            price = close;
            if (index == 900)
                continue;
            var openTime = T0 + TimeSpan.FromHours(index);
            store.Candles[openTime] = new Candle("Binance", "BTCUSDT", CandleInterval.H1, openTime, open, high, low,
                close, 1m, true, T0);
        }

        return store;
    }

    private static string D(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "null";

    private static async Task<string> RenderAsync()
    {
        var candles = BuildCandles();
        var sb = new StringBuilder();

        // A. Tín hiệu EMA qua đúng đường chạy của worker.
        var instance = new StrategyInstance("golden", StrategyTypes.EmaCross,
            StrategyFactory.Serialize(new EmaCrossParameters(9, 21, 300)), "Binance", "BTCUSDT", "1h", true);
        typeof(StrategyInstance).GetProperty(nameof(StrategyInstance.Id))!.SetValue(instance, 1);
        var signals = new InMemorySignalStore();
        var clock = new FakeClock(T0 + TimeSpan.FromDays(400));
        await new SignalEvaluationService(candles, signals, clock, new SignalOptions(),
            NullLogger<SignalEvaluationService>.Instance).EvaluatePendingAsync(instance, default);
        sb.AppendLine("# EMA signals");
        foreach (var signal in signals.Signals)
            sb.AppendLine($"{signal.CandleOpenTimeUtc:O} {signal.Action} {D(signal.CandleClose)} {D(signal.FastEma)} {D(signal.SlowEma)} {signal.IsBackfill} {signal.Reason}");

        // B. Giá trị EMA trên chart.
        var chart = await new ChartQueryService(new SingleInstanceStore(instance), candles, signals)
            .GetAsync(1, 1000, default);
        sb.AppendLine("# EMA chart");
        foreach (var point in chart!.Points)
            sb.AppendLine($"{point.Candle.OpenTimeUtc:O} {D(point.FastEma)} {D(point.SlowEma)}");

        // C. Chỉ báo MyRsi, có và không có bộ lọc xu hướng.
        var registry = new IndicatorRegistry(
        [
            new IndicatorDefinition { Name = "rsi", Type = "MyRsi", Symbol = "BTCUSDT", Interval = "1h" },
            new IndicatorDefinition
            {
                Name = "rsi-trend", Type = "MyRsi", Symbol = "BTCUSDT", Interval = "1h",
                Parameters = new(StringComparer.OrdinalIgnoreCase) { ["TrendEmaPeriod"] = "50", ["Period"] = "7" },
            },
        ], NullLogger<IndicatorRegistry>.Instance);
        var indicators = new IndicatorService(registry, candles);
        foreach (var name in new[] { "rsi", "rsi-trend" })
        {
            sb.AppendLine($"# Indicator {name}");
            var ic = await indicators.GetChartAsync(name, 1000, default);
            foreach (var goldenPoint in ic!.Points)
            {
                var values = string.Join(",", goldenPoint.Result.Values.OrderBy(kv => kv.Key).Select(kv =>
                    $"{kv.Key}={D(kv.Value)}"));
                sb.AppendLine($"{goldenPoint.Candle.OpenTimeUtc:O} {goldenPoint.Result.Signal} {values} {goldenPoint.Result.Reason}");
            }

            var latest = await indicators.GetLatestAsync(name, default);
            sb.AppendLine($"latest {latest!.Candle.OpenTimeUtc:O} {latest.Result.Signal} {latest.Result.Reason}");
        }

        return sb.ToString();
    }

    private static string GoldenPath([CallerFilePath] string here = "") =>
        Path.Combine(Path.GetDirectoryName(here)!, "Golden", "signal_outputs.txt");

    [Fact]
    public async Task Signal_outputs_match_golden_snapshot()
    {
        var actual = await RenderAsync();
        var path = GoldenPath();

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, actual);
        }

        var expected = await File.ReadAllTextAsync(path);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Golden_data_exercises_signals_and_gap_paths()
    {
        var text = await RenderAsync();

        Assert.Contains(" Buy ", text);
        Assert.Contains(" Sell ", text);
        Assert.Contains(" Long ", text);
        Assert.Contains(" Short ", text);
        Assert.Contains("Thiếu nến", text);
    }
}
