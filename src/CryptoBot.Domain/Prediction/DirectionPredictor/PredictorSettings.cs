namespace CryptoBot.Domain.Prediction;

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
