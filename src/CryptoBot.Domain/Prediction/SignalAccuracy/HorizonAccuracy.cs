namespace CryptoBot.Domain.Prediction;

/// <summary>
/// Kết quả các tín hiệu sau <paramref name="Candles"/> nến: đúng khi giá đóng lúc đó đi đúng hướng so với
/// giá đóng của nến phát tín hiệu.
/// </summary>
/// <param name="Candles">Số nến sau tín hiệu.</param>
/// <param name="Signals">Số tín hiệu đã có đủ nến phía sau để chấm (giá không đổi thì bỏ qua).</param>
/// <param name="Correct">Số tín hiệu đi đúng hướng.</param>
/// <param name="AverageMovePercent">
/// Trung bình % giá đi theo hướng tín hiệu: dương là trung bình đi đúng hướng, âm là đi ngược.
/// </param>
public sealed record HorizonAccuracy(int Candles, int Signals, int Correct, decimal AverageMovePercent)
{
    /// <summary>Tỷ lệ đúng (%); 0 khi chưa có tín hiệu nào được chấm.</summary>
    public decimal HitRatePercent => Signals == 0 ? 0 : (decimal)Correct / Signals * 100;
}
