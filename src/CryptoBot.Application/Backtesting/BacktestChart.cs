using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Application.Backtesting;

/// <summary>
/// Dữ liệu vẽ chart cho kết quả backtest. Nhiều năm nến 1h/15m là hàng chục đến hàng trăm nghìn nến, quá nặng để
/// gửi và vẽ, nên gộp các nến liên tiếp thành tối đa <c>maxBars</c> cột (OHLC đúng của cả nhóm). Số liệu thống kê
/// luôn tính trên từng nến gốc; chỉ phần hiển thị được gộp.
/// </summary>
public sealed record BacktestChart(
    int CandlesPerBar,
    IReadOnlyList<BacktestChartBar> Bars,
    IReadOnlyList<BacktestChartMarker> Markers)
{
    public static BacktestChart Build(IReadOnlyList<Candle> candles, BacktestResult result, int maxBars)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBars, 1);
        if (candles.Count == 0)
            return new BacktestChart(1, [], []);

        var perBar = (candles.Count + maxBars - 1) / maxBars;
        var equity = result.Equity.ToDictionary(point => point.CandleOpenTimeUtc, point => point.Equity);
        var bars = new List<BacktestChartBar>((candles.Count + perBar - 1) / perBar);

        for (var start = 0; start < candles.Count; start += perBar)
        {
            var end = Math.Min(start + perBar, candles.Count) - 1;
            var first = candles[start];
            var high = first.High;
            var low = first.Low;
            for (var index = start; index <= end; index++)
            {
                high = Math.Max(high, candles[index].High);
                low = Math.Min(low, candles[index].Low);
            }

            var last = candles[end];
            bars.Add(new BacktestChartBar(first.OpenTimeUtc, first.Open, high, low, last.Close,
                equity.TryGetValue(last.OpenTimeUtc, out var value) ? value : null));
        }

        var markers = new List<BacktestChartMarker>(result.Trades.Count * 2 + 1);
        foreach (var trade in result.Trades)
        {
            if (BarTimeOf(trade.EntryTimeUtc) is { } entryBar)
                markers.Add(new BacktestChartMarker(entryBar, true, trade.EntryPrice, null, null));
            if (BarTimeOf(trade.ExitTimeUtc) is { } exitBar)
                markers.Add(new BacktestChartMarker(exitBar, false, trade.ExitPrice, trade.ExitReason, trade.NetPnl));
        }

        if (result.OpenTrade is { } open && BarTimeOf(open.EntryTimeUtc) is { } openBar)
            markers.Add(new BacktestChartMarker(openBar, true, open.EntryPrice, null, null));

        return new BacktestChart(perBar, bars, markers);

        // Cột chứa nến có OpenTime này; null nếu nến không thuộc khoảng (ví dụ nến warm-up).
        // Tìm nhị phân trên nến đã sắp tăng dần: chỉ vài trăm marker, không cần bảng cho mọi nến.
        DateTime? BarTimeOf(DateTime openTimeUtc)
        {
            var low = 0;
            var high = candles.Count - 1;
            while (low <= high)
            {
                var middle = low + (high - low) / 2;
                var compared = candles[middle].OpenTimeUtc.CompareTo(openTimeUtc);
                if (compared == 0)
                    return bars[middle / perBar].OpenTimeUtc;
                if (compared < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            return null;
        }
    }
}
