using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Strategies;

namespace CryptoBot.Application.Signals;

public sealed record ChartPoint(Candle Candle, decimal? FastEma, decimal? SlowEma);

public sealed record ChartData(StrategyInstance Instance, EmaCrossParameters Parameters,
    IReadOnlyList<ChartPoint> Points, IReadOnlyList<TradingSignal> Signals);

/// <summary>
/// Dữ liệu chart cho một strategy instance. EMA được tính ở backend bằng đúng quy tắc
/// cửa sổ của chiến lược, nên đường EMA trên UI khớp với giá trị đã dùng để ra signal.
/// </summary>
public sealed class ChartQueryService(IStrategyInstanceStore instances, ICandleStore candles, ISignalStore signals)
{
    public const int MaxLimit = 1000;

    public async Task<ChartData?> GetAsync(int instanceId, int limit, CancellationToken ct)
    {
        var instance = await instances.GetAsync(instanceId, ct);
        if (instance is null)
            return null;

        limit = Math.Clamp(limit, 1, MaxLimit);
        var strategy = StrategyFactory.Create(instance);
        var parameters = StrategyFactory.ParseEmaCross(instance.ParametersJson);
        var key = new CandleKey(instance.Exchange, instance.Symbol, CandleInterval.Parse(instance.Interval));

        var visible = await candles.GetLatestAsync(key, null, limit, closedOnly: false, ct);
        if (visible.Count == 0)
            return new ChartData(instance, parameters, [], []);

        var history = await candles.GetLatestAsync(key, visible[0].OpenTimeUtc, strategy.RequiredCandles - 1,
            closedOnly: true, ct);
        var points = BuildPoints(strategy, history, visible);

        var sigs = await signals.QueryAsync(instance.Id, instance.Version, visible[0].OpenTimeUtc, null,
            MaxLimit, ct);
        return new ChartData(instance, parameters, points, sigs);
    }

    /// <summary>
    /// EMA cho từng nến hiển thị. Giá đóng cửa của chuỗi nến đã đóng được lấy một lần; mỗi nến chỉ
    /// truyền một đoạn (span) kết thúc tại nến đó vào chiến lược, không sao chép cửa sổ.
    /// </summary>
    private static List<ChartPoint> BuildPoints(EmaCrossStrategy strategy, IReadOnlyList<Candle> history,
        IReadOnlyList<Candle> visible)
    {
        var closes = ClosedCloses(history, visible);
        var points = new List<ChartPoint>(visible.Count);
        var closedIndex = history.Count - 1;
        foreach (var c in visible)
        {
            if (!c.IsClosed)
            {
                // Nến đang chạy không có EMA: chiến lược chỉ dùng nến đã đóng.
                points.Add(new ChartPoint(c, null, null));
                continue;
            }

            closedIndex++;
            var start = Math.Max(0, closedIndex - strategy.RequiredCandles + 1);
            var ema = strategy.ComputeAt(closes.AsSpan(start, closedIndex - start + 1));
            points.Add(new ChartPoint(c, ema?.Fast, ema?.Slow));
        }

        return points;
    }

    /// <summary>Giá đóng cửa của lịch sử + các nến hiển thị đã đóng, theo thứ tự thời gian.</summary>
    private static decimal[] ClosedCloses(IReadOnlyList<Candle> history, IReadOnlyList<Candle> visible)
    {
        var closes = new List<decimal>(history.Count + visible.Count);
        foreach (var c in history)
            closes.Add(c.Close);
        foreach (var c in visible)
            if (c.IsClosed)
                closes.Add(c.Close);
        return closes.ToArray();
    }
}
