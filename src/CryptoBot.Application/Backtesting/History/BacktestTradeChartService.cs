using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Signals;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Backtesting;

/// <summary>
/// Chart chi tiết một lệnh của lần backtest đã lưu: nến thật (không gộp như chart tổng) từ trước lúc vào đến sau lúc
/// ra, kèm EMA nhanh/chậm tính đúng như lúc bot ra tín hiệu. Đọc lại nến trong DB, không lưu thêm gì.
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

    /// <summary>Số nến tối đa đọc từ DB cho một lệnh.</summary>
    public const int MaxCandles = 50_000;

    /// <summary>
    /// Chart của lệnh thứ <paramref name="tradeIndex"/> (theo thứ tự trong kết quả; bằng số lệnh đã đóng = lệnh còn
    /// mở ở cuối kỳ). Null nếu không có lần chạy hoặc không có lệnh ở vị trí đó.
    /// </summary>
    /// <exception cref="ArgumentException">Chiến lược của lần chạy không còn, nên không biết đọc nến của sàn nào.</exception>
    public async Task<BacktestTradeChart?> GetAsync(long backtestId, int tradeIndex, CancellationToken ct)
    {
        if (await history.GetAsync(backtestId, ct) is not { } run)
            return null;

        var snapshot = run.Snapshot;
        DateTime entry, exit;
        if (tradeIndex >= 0 && tradeIndex < snapshot.Trades.Count)
            (entry, exit) = (snapshot.Trades[tradeIndex].EntryTimeUtc, snapshot.Trades[tradeIndex].ExitTimeUtc);
        else if (tradeIndex == snapshot.Trades.Count && snapshot.OpenTrade is { } open)
            (entry, exit) = (open.EntryTimeUtc, snapshot.LastCandleUtc ?? open.EntryTimeUtc);
        else
            return null;

        if (await instances.GetAsync(run.Saved.StrategyInstanceId, ct) is not { } instance)
            throw new ArgumentException("Chiến lược của lần chạy này không còn, không đọc được nến.");

        // Cặp và khung nến lấy theo lần chạy (instance có thể đã đổi cấu hình sau đó); sàn lấy theo instance.
        var key = new CandleKey(instance.Exchange, run.Saved.Symbol, CandleInterval.Parse(run.Saved.Interval));
        var strategy = new EmaCrossStrategy(snapshot.Parameters);
        var step = key.Interval.Duration;
        var from = entry - step * ContextCandles;
        var span = Math.Min(MaxCandles, (int)((exit - from).Ticks / step.Ticks) + 1 + ContextCandles);

        var warmup = await candles.GetLatestAsync(key, from, strategy.RequiredCandles - 1, closedOnly: true, ct);
        var window = await candles.GetAfterAsync(key, from.AddTicks(-1), span, closedOnly: true, ct);
        var series = new List<Candle>(warmup.Count + window.Count);
        series.AddRange(warmup);
        series.AddRange(window);

        var (perBar, bars) = BacktestChart.GroupBars(series, warmup.Count, MaxBars, strategy, _ => null);
        return new BacktestTradeChart(perBar, bars);
    }
}
