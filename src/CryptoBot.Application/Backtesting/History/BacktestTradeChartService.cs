using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Backtesting;

/// <summary>
/// Chart chi tiết một lệnh của lần backtest đã lưu: nến thật (không gộp như chart tổng) từ trước lúc vào đến sau lúc
/// ra, kèm EMA nhanh/chậm tính đúng như lúc bot ra tín hiệu. Chỉ đọc thông số của lần chạy (không giải nén kết quả)
/// và nến trong DB; giờ vào/ra do dashboard gửi từ danh sách lệnh nó đã có.
/// </summary>
public sealed class BacktestTradeChartService(
    BacktestHistoryService history,
    IStrategyInstanceStore instances,
    ICandleStore candles)
{
    /// <summary>Số nến hiển thị thêm trước lúc vào và sau lúc ra, để thấy bối cảnh.</summary>
    public const int ContextCandles = 30;

    /// <summary>Số cột tối đa; lệnh giữ rất lâu thì gộp nến giống chart tổng.</summary>
    public const int MaxBars = 1500;

    /// <summary>
    /// Chart từ <paramref name="entryUtc"/> đến <paramref name="exitUtc"/> (lệnh còn mở thì exit là nến cuối của lần
    /// chạy). Null nếu không có lần chạy.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Giờ vào/ra sai hoặc nằm ngoài khoảng của lần chạy, hoặc chiến lược của lần chạy không còn.
    /// </exception>
    public async Task<BacktestTradeChart?> GetAsync(long backtestId, DateTime entryUtc, DateTime exitUtc,
        CancellationToken ct)
    {
        if (await history.GetSummaryAsync(backtestId, ct) is not { } run)
            return null;
        if (entryUtc > exitUtc)
            throw new ArgumentException("Giờ vào phải trước giờ ra.");
        // Lệnh luôn nằm trong khoảng của lần chạy (tối đa BacktestService.MaxCandles nến), nên đọc đủ tới sau lúc ra.
        if (run.FirstCandleUtc is not { } first || run.LastCandleUtc is not { } last || entryUtc < first
            || exitUtc > last)
            throw new ArgumentException("Giờ vào/ra nằm ngoài khoảng nến của lần chạy này.");

        if (await instances.GetAsync(run.StrategyInstanceId, ct) is not { } instance)
            throw new ArgumentException("Chiến lược của lần chạy này không còn, không đọc được nến.");

        // Cặp và khung nến lấy theo lần chạy (instance có thể đã đổi cấu hình sau đó); sàn lấy theo instance.
        var key = new CandleKey(instance.Exchange, run.Symbol, CandleInterval.Parse(run.Interval));
        var strategy = new EmaCrossStrategy(new EmaCrossParameters(run.FastPeriod, run.SlowPeriod,
            run.LookbackCandles));
        var step = key.Interval.Duration;
        var from = entryUtc - step * ContextCandles;
        var span = (int)((exitUtc - from).Ticks / step.Ticks) + 1 + ContextCandles;

        var warmup = await candles.GetLatestAsync(key, from, strategy.RequiredCandles - 1, closedOnly: true, ct);
        var window = await candles.GetAfterAsync(key, from.AddTicks(-1), span, closedOnly: true, ct);
        var series = new List<Candle>(warmup.Count + window.Count);
        series.AddRange(warmup);
        series.AddRange(window);

        var (perBar, bars) = BacktestChart.GroupBars(series, warmup.Count, key.Interval, MaxBars, strategy, _ => null);
        return new BacktestTradeChart(perBar, bars);
    }
}
