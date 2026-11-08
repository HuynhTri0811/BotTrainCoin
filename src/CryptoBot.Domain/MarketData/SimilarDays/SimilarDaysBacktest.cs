namespace CryptoBot.Domain.MarketData;

/// <summary>Kết quả chạy thử walk-forward của <see cref="SimilarDaysForecast"/>.</summary>
/// <param name="StrongDays">Số ngày dự báo nghiêng rõ (xác suất tăng ≥ 60% hoặc ≤ 40%).</param>
public sealed record SimilarDaysBacktest(int Days, int Correct, int StrongDays, int StrongCorrect)
{
    public decimal HitRatePercent => Days == 0 ? 0 : (decimal)Correct / Days * 100;
    public decimal StrongHitRatePercent => StrongDays == 0 ? 0 : (decimal)StrongCorrect / StrongDays * 100;
}
