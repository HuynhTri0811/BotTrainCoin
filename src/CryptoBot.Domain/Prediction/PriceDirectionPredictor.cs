using CryptoBot.Domain.Common;
using CryptoBot.Domain.MarketData;

namespace CryptoBot.Domain.Prediction;

/// <summary>Kết quả dự đoán. Score &gt;= 0 là Up, &lt; 0 là Down; |Score| càng lớn càng "chắc".</summary>
public sealed record PricePrediction(PriceDirection Direction, decimal Score, string Reason);

/// <summary>Kết quả chạy thử trên lịch sử: đoán bao nhiêu lần, đúng bao nhiêu lần.</summary>
public sealed record PredictionAccuracy(int Predictions, int Correct)
{
    /// <summary>Tỷ lệ đoán đúng (%); 0 khi chưa có lần đoán nào.</summary>
    public decimal HitRatePercent => Predictions == 0 ? 0 : (decimal)Correct / Predictions * 100;
}

/// <summary>
/// Trọng số và tham số của từng thành phần trong điểm dự đoán. Sửa ở đây để tối ưu;
/// đặt trọng số = 0 để tắt một thành phần.
/// </summary>
public sealed record PredictorSettings
{
    /// <summary>Số nến của đường trung bình dùng cho thành phần xu hướng.</summary>
    public int TrendPeriod { get; init; } = 20;

    /// <summary>Số nến nhìn lại cho thành phần đà (momentum).</summary>
    public int MomentumPeriod { get; init; } = 3;

    /// <summary>Trọng số: giá đóng nằm trên/dưới trung bình bao nhiêu %.</summary>
    public decimal TrendWeight { get; init; } = 1m;

    /// <summary>Trọng số: giá đã đi bao nhiêu % trong <see cref="MomentumPeriod"/> nến gần nhất.</summary>
    public decimal MomentumWeight { get; init; } = 1m;

    /// <summary>Trọng số: mua chủ động chiếm hơn/kém 50% bao nhiêu điểm % ở nến cuối (bỏ qua nếu thiếu dữ liệu).</summary>
    public decimal PressureWeight { get; init; } = 0.05m;

    /// <summary>Số nến đã đóng tối thiểu cần để dự đoán.</summary>
    public int RequiredCandles => Math.Max(TrendPeriod, MomentumPeriod + 1);
}

/// <summary>
/// Hàm core: nhìn các nến đã đóng gần nhất và trả lời nến kế tiếp sẽ đóng cao hơn (Up) hay thấp hơn (Down)
/// giá đóng của nến cuối. Điểm = tổng có trọng số của các thành phần; mỗi thành phần dương nghiêng về Up.
/// Đây là bản khởi đầu để tối ưu dần; đo bằng <see cref="Backtest"/> trước khi tin vào nó.
/// </summary>
public static class PriceDirectionPredictor
{
    /// <summary>Dự đoán từ <paramref name="closedCandles"/> (tăng dần theo thời gian, chỉ nến đã đóng).</summary>
    public static PricePrediction Predict(IReadOnlyList<Candle> closedCandles, PredictorSettings? settings = null)
    {
        PredictorSettings s = settings ?? new PredictorSettings();
        if (closedCandles.Count < s.RequiredCandles)
            throw new ArgumentException($"Cần ít nhất {s.RequiredCandles} nến đã đóng.", nameof(closedCandles));
        if (closedCandles.Any(c => !c.IsClosed))
            throw new ArgumentException("Chỉ dùng nến đã đóng.", nameof(closedCandles));

        Candle last = closedCandles[^1];

        // 1. Xu hướng: giá trên trung bình → nghiêng lên.
        decimal average = closedCandles.TakeLast(s.TrendPeriod).Average(c => c.Close);
        decimal trend = PercentChange.Between(average, last.Close);

        // 2. Đà: mấy nến gần nhất đang đi lên → nghiêng lên.
        decimal before = closedCandles[^(s.MomentumPeriod + 1)].Close;
        decimal momentum = PercentChange.Between(before, last.Close);

        // 3. Lực mua/bán chủ động ở nến cuối: mua > 50% → nghiêng lên.
        VolumePressure? pressure = VolumePressure.From(last);
        decimal pressureScore = pressure is null ? 0 : pressure.BuyPercent - 50;

        decimal score = s.TrendWeight * trend + s.MomentumWeight * momentum + s.PressureWeight * pressureScore;
        PriceDirection direction = score >= 0 ? PriceDirection.Up : PriceDirection.Down;
        string reason = $"Xu hướng {trend:+0.###;-0.###;0}% × {s.TrendWeight}, " +
                        $"đà {momentum:+0.###;-0.###;0}% × {s.MomentumWeight}, " +
                        (pressure is null ? "lực mua/bán: chưa có dữ liệu" :
                            $"mua {pressure.BuyPercent:0.#}% × {s.PressureWeight}") +
                        $" → điểm {score:+0.####;-0.####;0}";
        return new PricePrediction(direction, score, reason);
    }

    /// <summary>
    /// Chạy thử trên lịch sử: với mỗi nến từ vị trí <see cref="PredictorSettings.RequiredCandles"/>,
    /// chỉ dùng các nến trước nó để đoán, rồi so với thực tế (đóng cao hơn nến trước = Up).
    /// Nến đóng bằng đúng giá nến trước bị bỏ qua.
    /// </summary>
    public static PredictionAccuracy Backtest(IReadOnlyList<Candle> closedCandles, PredictorSettings? settings = null)
    {
        PredictorSettings s = settings ?? new PredictorSettings();
        int predictions = 0;
        int correct = 0;
        for (int i = s.RequiredCandles; i < closedCandles.Count; i++)
        {
            decimal previousClose = closedCandles[i - 1].Close;
            decimal actualClose = closedCandles[i].Close;
            if (actualClose == previousClose)
                continue;

            List<Candle> window = closedCandles.Skip(i - s.RequiredCandles).Take(s.RequiredCandles).ToList();
            PriceDirection predicted = Predict(window, s).Direction;
            PriceDirection actual = actualClose > previousClose ? PriceDirection.Up : PriceDirection.Down;
            predictions++;
            if (predicted == actual)
                correct++;
        }

        return new PredictionAccuracy(predictions, correct);
    }
}
