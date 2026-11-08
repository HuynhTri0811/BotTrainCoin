namespace CryptoBot.Domain.Prediction;

/// <summary>Kết quả chạy thử trên lịch sử: đoán bao nhiêu lần, đúng bao nhiêu lần.</summary>
public sealed record PredictionAccuracy(int Predictions, int Correct)
{
    /// <summary>Tỷ lệ đoán đúng (%); 0 khi chưa có lần đoán nào.</summary>
    public decimal HitRatePercent => Predictions == 0 ? 0 : (decimal)Correct / Predictions * 100;
}
