using System.Text.Json;
using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Backtesting;

/// <summary>
/// Chạy <see cref="Backtester"/> trên nến đã lưu của một strategy instance. Chỉ đọc DB, không gọi sàn: thiếu lịch sử
/// thì tải trước bằng <see cref="MarketData.CandleBackfillService"/>.
/// </summary>
public sealed class BacktestService(IStrategyInstanceStore instances, ICandleStore candles)
{
    /// <summary>Số nến tối đa mỗi lần chạy (≈ 45 năm nến 1h, ≈ 11 năm nến 15m).</summary>
    public const int MaxCandles = 400_000;

    /// <summary>Số cột tối đa trên chart kết quả.</summary>
    public const int MaxChartBars = 3000;

    /// <summary>
    /// Cửa sổ EMA lớn nhất cho phép khi backtest. Mỗi nến tính lại EMA trên cả cửa sổ, nên thời gian chạy tăng theo
    /// (số nến × cửa sổ); nến warm-up đọc thêm cũng bằng cửa sổ và không tính vào <see cref="MaxCandles"/>.
    /// </summary>
    public const int MaxLookbackCandles = 1000;

    private const int PageSize = 50_000;

    /// <summary>Khoảng nến đã đóng có trong DB; null nếu không có instance.</summary>
    public async Task<BacktestCoverage?> GetCoverageAsync(int strategyId, CancellationToken ct)
    {
        if (await instances.GetAsync(strategyId, ct) is not { } instance)
            return null;

        var key = CandleKey.For(instance);
        var first = (await candles.GetAfterAsync(key, null, 1, closedOnly: true, ct)).FirstOrDefault();
        var last = (await candles.GetLatestAsync(key, null, 1, closedOnly: true, ct)).FirstOrDefault();
        return new BacktestCoverage(first?.OpenTimeUtc, last?.OpenTimeUtc);
    }

    /// <summary>Chạy backtest; null nếu không có instance.</summary>
    /// <exception cref="ArgumentException">
    /// Chiến lược không phải EMA cross, tham số chiến lược/vốn sai, khoảng thời gian sai, không có nến hoặc quá nhiều
    /// nến.
    /// </exception>
    /// <exception cref="OperationCanceledException">Request bị hủy; không có gì được trả về để lưu.</exception>
    public async Task<BacktestRun?> RunAsync(BacktestRequest request, CancellationToken ct)
    {
        if (await instances.GetAsync(request.StrategyId, ct) is not { } instance)
            return null;
        if (request.FromUtc is { } from && request.ToUtc is { } to && from > to)
            throw new ArgumentException("Ngày bắt đầu phải trước ngày kết thúc.");
        if (request.Settings.Validate() is { } settingsError)
            throw new ArgumentException(settingsError);

        var defaults = ParseParameters(instance);
        var parameters = request.Overrides?.ApplyTo(defaults) ?? defaults;
        if (parameters.Validate() is { } parametersError)
            throw new ArgumentException(parametersError);
        if (parameters.LookbackCandles > MaxLookbackCandles)
            throw new ArgumentException($"LookbackCandles tối đa {MaxLookbackCandles:N0} khi backtest.");
        var strategy = new EmaCrossStrategy(parameters);
        var key = CandleKey.For(instance);

        var range = await LoadRangeAsync(key, request.FromUtc, request.ToUtc, ct);
        // Không có nến thì không có gì để chạy hay lưu vào lịch sử.
        if (range.Count == 0)
            throw new ArgumentException(
                "Không có nến nào trong khoảng đã chọn; hãy chọn khoảng khác hoặc tải thêm lịch sử.");
        // Nến trước khoảng chỉ để có đủ cửa sổ cho nến đầu, giống SignalEvaluationService.
        var warmup = await candles.GetLatestAsync(key, range[0].OpenTimeUtc, strategy.RequiredCandles - 1,
            closedOnly: true, ct);

        var series = new List<Candle>(warmup.Count + range.Count);
        series.AddRange(warmup);
        series.AddRange(range);
        var result = Backtester.Run(series, key.Interval, strategy, request.Settings, ct);

        return new BacktestRun(instance, parameters, range[0].OpenTimeUtc, range[^1].OpenTimeUtc, range.Count,
            warmup.Count, result,
            BacktestChart.Build(series, warmup.Count, key.Interval, result, strategy, MaxChartBars));
    }

    /// <summary>Nến đã đóng có OpenTime trong [from, to], tăng dần, đọc theo trang.</summary>
    private async Task<List<Candle>> LoadRangeAsync(CandleKey key, DateTime? fromUtc, DateTime? toUtc,
        CancellationToken ct)
    {
        var loaded = new List<Candle>();
        DateTime? after = fromUtc?.AddTicks(-1);
        while (true)
        {
            var page = await candles.GetAfterAsync(key, after, PageSize, closedOnly: true, ct);
            foreach (var candle in page)
            {
                if (toUtc is { } to && candle.OpenTimeUtc > to)
                    return loaded;
                // Kiểm tra từng nến: trả về sớm ở ToUtc cũng không được vượt giới hạn.
                if (loaded.Count == MaxCandles)
                    throw new ArgumentException(
                        $"Khoảng thời gian có hơn {MaxCandles:N0} nến; hãy chọn khoảng ngắn hơn.");
                loaded.Add(candle);
            }

            if (page.Count < PageSize)
                return loaded;
            after = page[^1].OpenTimeUtc;
        }
    }

    /// <summary>
    /// Tham số EMA cross của instance; lỗi loại chiến lược hoặc JSON hỏng thành <see cref="ArgumentException"/>.
    /// </summary>
    private static EmaCrossParameters ParseParameters(StrategyInstance instance)
    {
        // Giống StrategyFactory.Create: chỉ chạy được EMA cross, không đọc tham số loại khác như EMA cross.
        if (instance.StrategyType != StrategyTypes.EmaCross)
            throw new ArgumentException($"Backtest chưa hỗ trợ loại chiến lược {instance.StrategyType}.");
        try
        {
            return StrategyFactory.ParseEmaCross(instance.ParametersJson);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Tham số của chiến lược không đọc được: {ex.Message}", ex);
        }
    }
}
