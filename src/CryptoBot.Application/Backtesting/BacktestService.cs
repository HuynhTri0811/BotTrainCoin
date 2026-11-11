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

    private const int PageSize = 50_000;

    /// <summary>Khoảng nến đã đóng có trong DB; null nếu không có instance.</summary>
    public async Task<BacktestCoverage?> GetCoverageAsync(int strategyId, CancellationToken ct)
    {
        if (await instances.GetAsync(strategyId, ct) is not { } instance)
            return null;

        var key = KeyOf(instance);
        var first = (await candles.GetAfterAsync(key, null, 1, closedOnly: true, ct)).FirstOrDefault();
        var last = (await candles.GetLatestAsync(key, null, 1, closedOnly: true, ct)).FirstOrDefault();
        return new BacktestCoverage(first?.OpenTimeUtc, last?.OpenTimeUtc);
    }

    /// <summary>Chạy backtest; null nếu không có instance.</summary>
    /// <exception cref="ArgumentException">Tham số chiến lược/vốn sai, khoảng thời gian sai hoặc quá nhiều nến.</exception>
    public async Task<BacktestRun?> RunAsync(BacktestRequest request, CancellationToken ct)
    {
        if (await instances.GetAsync(request.StrategyId, ct) is not { } instance)
            return null;
        if (request.FromUtc is { } from && request.ToUtc is { } to && from > to)
            throw new ArgumentException("Ngày bắt đầu phải trước ngày kết thúc.");
        if (request.Settings.Validate() is { } settingsError)
            throw new ArgumentException(settingsError);

        var defaults = StrategyFactory.ParseEmaCross(instance.ParametersJson);
        var parameters = request.Overrides?.ApplyTo(defaults) ?? defaults;
        if (parameters.Validate() is { } parametersError)
            throw new ArgumentException(parametersError);
        var strategy = new EmaCrossStrategy(parameters);
        var key = KeyOf(instance);

        var range = await LoadRangeAsync(key, request.FromUtc, request.ToUtc, ct);
        // Nến trước khoảng chỉ để có đủ cửa sổ cho nến đầu, giống SignalEvaluationService.
        var warmup = range.Count == 0
            ? []
            : await candles.GetLatestAsync(key, range[0].OpenTimeUtc, strategy.RequiredCandles - 1,
                closedOnly: true, ct);

        var series = new List<Candle>(warmup.Count + range.Count);
        series.AddRange(warmup);
        series.AddRange(range);
        var result = Backtester.Run(series, key.Interval, strategy, request.Settings);

        return new BacktestRun(instance, parameters, range.FirstOrDefault()?.OpenTimeUtc,
            range.LastOrDefault()?.OpenTimeUtc, range.Count, warmup.Count, result,
            BacktestChart.Build(range, result, MaxChartBars));
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

    private static CandleKey KeyOf(StrategyInstance instance) =>
        new(instance.Exchange, instance.Symbol, CandleInterval.Parse(instance.Interval));
}
