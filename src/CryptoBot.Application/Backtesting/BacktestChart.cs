using CryptoBot.Domain.Backtesting;
using CryptoBot.Domain.Common;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Backtesting;

/// <summary>
/// Dữ liệu vẽ chart cho kết quả backtest. Nhiều năm nến 1h/15m là hàng chục đến hàng trăm nghìn nến, quá nặng để
/// gửi và vẽ, nên gộp các nến liên tiếp thành tối đa <c>maxBars</c> cột (OHLC đúng của cả nhóm). Số liệu thống kê
/// luôn tính trên từng nến gốc; chỉ phần hiển thị được gộp. Điểm vào/ra do dashboard đặt từ danh sách lệnh.
/// </summary>
public sealed record BacktestChart(int CandlesPerBar, IReadOnlyList<BacktestChartBar> Bars)
{
    /// <param name="series">Nến warm-up rồi tới nến của khoảng chạy, tăng dần (đúng chuỗi đưa vào Backtester).</param>
    /// <param name="rangeStart">Vị trí nến đầu của khoảng chạy trong <paramref name="series"/>.</param>
    /// <param name="interval">Khung nến, để nhận ra cửa sổ EMA có nến thiếu.</param>
    /// <param name="result">Kết quả backtest, để lấy tổng vốn.</param>
    /// <param name="strategy">Chiến lược đã chạy, để vẽ EMA đúng như lúc ra tín hiệu.</param>
    /// <param name="maxBars">Số cột tối đa.</param>
    public static BacktestChart Build(IReadOnlyList<Candle> series, int rangeStart, CandleInterval interval,
        BacktestResult result, EmaCrossStrategy strategy, int maxBars)
    {
        var equity = result.Equity.ToDictionary(point => point.CandleOpenTimeUtc, point => point.Equity);
        var (perBar, bars) = GroupBars(series, rangeStart, interval, maxBars, strategy,
            openTimeUtc => equity.TryGetValue(openTimeUtc, out var value) ? value : null);
        return new BacktestChart(perBar, bars);
    }

    /// <summary>
    /// Gộp nến từ <paramref name="start"/> tới hết <paramref name="series"/> thành tối đa <paramref name="maxBars"/>
    /// cột, kèm EMA của <paramref name="strategy"/> tại nến cuối mỗi cột. Nến trước <paramref name="start"/> chỉ
    /// dùng để có đủ cửa sổ EMA.
    /// </summary>
    /// <remarks>
    /// EMA để trống khi cửa sổ kết thúc tại nến cuối của cột thiếu nến hoặc chứa khoảng thiếu nến: bot và Backtester
    /// không đánh giá các cửa sổ đó (<see cref="CandleWindow.HasGap"/>), nên chart cũng không vẽ EMA ở đó.
    /// </remarks>
    /// <param name="equityAt">Tổng vốn tại nến cuối của cột (theo OpenTime), null nếu không có.</param>
    public static (int CandlesPerBar, List<BacktestChartBar> Bars) GroupBars(IReadOnlyList<Candle> series, int start,
        CandleInterval interval, int maxBars, EmaCrossStrategy strategy, Func<DateTime, decimal?> equityAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBars, 1);
        var count = series.Count - start;
        if (count <= 0)
            return (1, []);

        var perBar = (count + maxBars - 1) / maxBars;
        var window = strategy.RequiredCandles;
        var closes = new decimal[series.Count];
        for (var index = 0; index < series.Count; index++)
            closes[index] = series[index].Close;

        var bars = new List<BacktestChartBar>((count + perBar - 1) / perBar);
        for (var first = start; first < series.Count; first += perBar)
        {
            var last = Math.Min(first + perBar, series.Count) - 1;
            var high = series[first].High;
            var low = series[first].Low;
            for (var index = first; index <= last; index++)
            {
                high = Math.Max(high, series[index].High);
                low = Math.Min(low, series[index].Low);
            }

            // EMA trên cửa sổ kết thúc tại nến cuối của cột, giống lúc bot ra tín hiệu ở nến đó.
            var hasWindow = last + 1 >= window
                            && !CandleWindow.HasGap(new ListSlice<Candle>(series, last + 1 - window, window), interval);
            var ema = hasWindow ? strategy.ComputeAt(closes.AsSpan(0, last + 1)) : null;
            bars.Add(new BacktestChartBar(series[first].OpenTimeUtc, series[first].Open, high, low, series[last].Close,
                equityAt(series[last].OpenTimeUtc), ema?.Fast, ema?.Slow));
        }

        return (perBar, bars);
    }
}
