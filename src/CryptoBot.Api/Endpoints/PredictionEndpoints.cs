using CryptoBot.Application.Abstractions;
using CryptoBot.Application.LivePrices;
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
                var window = closed.Skip(i - settings.RequiredCandles).Take(settings.RequiredCandles).ToList();
                var p = PriceDirectionPredictor.Predict(window, settings);
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

