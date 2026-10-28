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
    private const int RecentCount = 20;

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
            for (var i = Math.Max(settings.RequiredCandles, closed.Count - RecentCount); i < closed.Count; i++)
            {
                var p = PriceDirectionPredictor.PredictAt(closed, i, settings);
                var previousClose = closed[i - 1].Close;
                var actual = closed[i].Close == previousClose ? null
                    : closed[i].Close > previousClose ? "Up" : "Down";
                recent.Add(new PredictionHistoryDto(ToUnixSeconds(closed[i].OpenTimeUtc), p.Direction.ToString(),
                    p.Score, actual, previousClose, closed[i].Close, closed[i].Close - previousClose,
                    (closed[i].Close / previousClose - 1) * 100));
            }
            recent.Reverse();

            return Results.Ok(new PredictionDto(symbol.ToUpperInvariant(), iv.Code,
                ToUnixSeconds(last.OpenTimeUtc + iv.Duration), last.Close, next.Direction.ToString(), next.Score,
                next.Reason, accuracy.Predictions, accuracy.Correct, accuracy.HitRatePercent, recent,
                new PredictionSettingsDto(settings.TrendPeriod, settings.MomentumPeriod, settings.TrendWeight,
                    settings.MomentumWeight, settings.PressureWeight)));
        });

        // Độ chính xác tín hiệu Long/Short của các chỉ báo tự viết cùng symbol/khung nến.
        app.MapGet("/api/prediction/indicators", async (string symbol, string interval, int? history,
            IndicatorRegistry registry, IndicatorService indicators, CancellationToken ct) =>
        {
            if (!CandleInterval.TryParse(interval, out var iv))
                return Results.BadRequest(new { error = $"Interval không hỗ trợ: {interval}" });

            var result = new List<IndicatorAccuracyDto>();
            foreach (var c in registry.All.Where(c =>
                         c.Key.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) && c.Key.Interval == iv))
            {
                var chart = await indicators.GetChartAsync(c.Name, history ?? IndicatorService.MaxLimit, ct);
                if (chart is null)
                    continue;

                var series = chart.Points.Select(p => p.Candle).ToList();
                var signals = chart.Points
                    .Select((p, i) => (p.Result, Index: i))
                    .Where(x => x.Result.Signal != IndicatorSignal.None)
                    .Select(x => new DirectionSignal(x.Index,
                        x.Result.Signal == IndicatorSignal.Long ? PriceDirection.Up : PriceDirection.Down))
                    .ToList();
                var horizons = SignalAccuracy.Evaluate(series, signals, SignalAccuracy.DefaultHorizons);

                var recent = signals.TakeLast(RecentCount).Reverse().Select(s => new IndicatorSignalDto(
                    ToUnixSeconds(series[s.Index].OpenTimeUtc), s.Direction.ToString(), series[s.Index].Close,
                    SignalAccuracy.MovePercent(series, s, 1), SignalAccuracy.MovePercent(series, s, 3),
                    chart.Points[s.Index].Result.Reason)).ToList();

                result.Add(new IndicatorAccuracyDto(c.Name, c.Type, series.Count,
                    series.Count > 0 ? ToUnixSeconds(series[0].OpenTimeUtc) : null,
                    horizons.Select(h => new HorizonAccuracyDto(h.Candles, h.Signals, h.Correct, h.HitRatePercent,
                        h.AverageMovePercent)).ToList(),
                    recent));
            }

            return Results.Ok(result);
        });
    }

    private static long ToUnixSeconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();
}

/// <summary>
/// Dự đoán nến mở lúc NextCandleTime đóng trên (Up) hay dưới (Down) LastClose,
/// kèm tỷ lệ đúng khi chạy thử trên các nến đã đóng đang xem.
/// </summary>
public sealed record PredictionDto(string Symbol, string Interval, long NextCandleTime, decimal LastClose,
    string Direction, decimal Score, string Reason, int BacktestPredictions, int BacktestCorrect,
    decimal HitRatePercent, IReadOnlyList<PredictionHistoryDto> Recent, PredictionSettingsDto Settings);

/// <summary>Actual là null khi nến đóng đúng bằng giá nến trước (không tính đúng/sai).</summary>
public sealed record PredictionHistoryDto(long Time, string Predicted, decimal Score, string? Actual,
    decimal PreviousClose, decimal Close, decimal Change, decimal ChangePercent);

public sealed record PredictionSettingsDto(int TrendPeriod, int MomentumPeriod, decimal TrendWeight,
    decimal MomentumWeight, decimal PressureWeight);

/// <summary>Tín hiệu Long (Up)/Short (Down) chấm trên Candles nến đã đóng gần nhất, bắt đầu từ FromTime.</summary>
public sealed record IndicatorAccuracyDto(string Name, string Type, int Candles, long? FromTime,
    IReadOnlyList<HorizonAccuracyDto> Horizons, IReadOnlyList<IndicatorSignalDto> Recent);

/// <summary>AverageMovePercent: trung bình % giá đi theo hướng tín hiệu (âm = đi ngược).</summary>
public sealed record HorizonAccuracyDto(int Candles, int Signals, int Correct, decimal HitRatePercent,
    decimal AverageMovePercent);

/// <summary>Move1/Move3: % giá đi theo hướng tín hiệu sau 1/3 nến; null khi chưa đủ nến.</summary>
public sealed record IndicatorSignalDto(long Time, string Direction, decimal Close, decimal? Move1, decimal? Move3,
    string Reason);
