using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Indicators;
using CryptoBot.Application.LivePrices;
using CryptoBot.Domain.Indicators;
using CryptoBot.Domain.MarketData;
using CryptoBot.Domain.Prediction;

namespace CryptoBot.Api.Endpoints;

public static class PredictionEndpoints
{
    /// <summary>Số lần đoán gần nhất trả về để so với thực tế.</summary>
    private const int RecentCount = 300;

    public static void MapPredictionEndpoints(this IEndpointRouteBuilder app)
    {
        // Gọi PriceDirectionPredictor với PredictorSettings mặc định: sửa hàm/tham số rồi restart API là thấy kết quả mới.
        // interval=1d: dự đoán theo ngày trên nến ngày lấy thẳng từ sàn (không lưu DB).
        app.MapGet("/api/prediction", async (string symbol, string interval, int? history, ICandleStore candles,
            DailyRangeService daily, CancellationToken ct) =>
        {
            if (!CandleInterval.TryParse(interval, out var iv))
                return Results.BadRequest(new { error = $"Interval không hỗ trợ: {interval}" });
            if (string.IsNullOrWhiteSpace(symbol) || !symbol.All(char.IsAsciiLetterOrDigit))
                return Results.BadRequest(new { error = "Symbol không hợp lệ" });

            var settings = new PredictorSettings();
            var limit = Math.Clamp(history ?? 300, 1, 1000) + settings.RequiredCandles;
            var closed = iv == CandleInterval.D1
                ? (await daily.GetClosedDailyCandlesAsync(symbol.ToUpperInvariant(), ct)).TakeLast(limit).ToList()
                : await candles.GetLatestAsync(new CandleKey("Binance", symbol.ToUpperInvariant(), iv), null, limit,
                    closedOnly: true, ct);
            if (closed.Count < settings.RequiredCandles)
                return Results.NotFound(new { error = $"Cần ít nhất {settings.RequiredCandles} nến đã đóng." });

            var last = closed[^1];
            var next = PriceDirectionPredictor.Predict(closed, settings);
            var accuracy = PriceDirectionPredictor.Backtest(closed, settings);

            // Các lần đoán gần nhất: chỉ dùng nến trước nến được đoán, rồi so với giá đóng thực tế.
            var recent = new List<PredictionHistoryDto>();
            for (var index = Math.Max(settings.RequiredCandles,
                closed.Count - RecentCount); index < closed.Count; index++)
            {
                var prediction = PriceDirectionPredictor.PredictAt(closed, index, settings);
                var previousClose = closed[index - 1].Close;
                var actual = closed[index].Close == previousClose ? null
                    : closed[index].Close > previousClose ? "Up" : "Down";
                recent.Add(new PredictionHistoryDto(ToUnixSeconds(closed[index].OpenTimeUtc),
                    prediction.Direction.ToString(),
                    prediction.Score, actual, previousClose, closed[index].Close, closed[index].Close - previousClose,
                    (closed[index].Close / previousClose - 1) * 100,
                    OpeningsIn(closed[index].OpenTimeUtc, iv)));
            }
            recent.Reverse();

            return Results.Ok(new PredictionDto(symbol.ToUpperInvariant(), iv.Code,
                ToUnixSeconds(last.OpenTimeUtc + iv.Duration), last.Close, next.Direction.ToString(), next.Score,
                next.Reason, accuracy.Predictions, accuracy.Correct, accuracy.HitRatePercent, recent,
                new PredictionSettingsDto(settings.TrendPeriod, settings.MomentumPeriod, settings.TrendWeight,
                    settings.MomentumWeight, settings.PressureWeight),
                OpeningsIn(last.OpenTimeUtc + iv.Duration, iv)));
        });

        // Độ chính xác tín hiệu Long/Short của các chỉ báo tự viết cùng symbol/khung nến.
        app.MapGet("/api/prediction/indicators", async (string symbol, string interval, int? history,
            IndicatorRegistry registry, IndicatorService indicators, CancellationToken ct) =>
        {
            if (!CandleInterval.TryParse(interval, out var iv))
                return Results.BadRequest(new { error = $"Interval không hỗ trợ: {interval}" });

            var result = new List<IndicatorAccuracyDto>();
            foreach (var indicator in registry.All.Where(candidate =>
                         candidate.Key.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)
                             && candidate.Key.Interval == iv))
            {
                var chart = await indicators.GetChartAsync(indicator.Name, history ?? IndicatorService.MaxLimit, ct);
                if (chart is null)
                    continue;

                var series = chart.Points.Select(point => point.Candle).ToList();
                var signals = chart.Points
                    .Select((point, index) => (point.Result, Index: index))
                    .Where(entry => entry.Result.Signal != IndicatorSignal.None)
                    .Select(entry => new DirectionSignal(entry.Index,
                        entry.Result.Signal == IndicatorSignal.Long ? PriceDirection.Up : PriceDirection.Down))
                    .ToList();
                var horizons = SignalAccuracy.Evaluate(series, signals, SignalAccuracy.DefaultHorizons);

                var recent = signals.TakeLast(RecentCount).Reverse().Select(signal => new IndicatorSignalDto(
                    ToUnixSeconds(series[signal.Index].OpenTimeUtc), signal.Direction.ToString(),
                        series[signal.Index].Close,
                    SignalAccuracy.MovePercent(series, signal, 1), SignalAccuracy.MovePercent(series, signal, 3),
                    chart.Points[signal.Index].Result.Reason)).ToList();

                result.Add(new IndicatorAccuracyDto(indicator.Name, indicator.Type, series.Count,
                    series.Count > 0 ? ToUnixSeconds(series[0].OpenTimeUtc) : null,
                    horizons.Select(accuracy => new HorizonAccuracyDto(accuracy.Candles, accuracy.Signals,
                        accuracy.Correct, accuracy.HitRatePercent,
                        accuracy.AverageMovePercent)).ToList(),
                    recent));
            }

            return Results.Ok(result);
        });
    }

    /// <summary>
    /// Các sàn chứng khoán mở cửa trong nến mở lúc <paramref name="openTimeUtc"/>. Nến ngày luôn chứa mọi phiên mở
    /// cửa trong tuần nên trả rỗng.
    /// </summary>
    private static IReadOnlyList<MarketOpeningDto> OpeningsIn(DateTime openTimeUtc, CandleInterval interval) =>
        interval == CandleInterval.D1
            ? []
            : MarketOpenings.Between(openTimeUtc, openTimeUtc + interval.Duration)
                .Select(opening => new MarketOpeningDto(opening.Market.Code, opening.Market.Name,
                    opening.Market.Exchange, ToUnixSeconds(opening.OpenUtc)))
                .ToList();

    private static long ToUnixSeconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();
}
