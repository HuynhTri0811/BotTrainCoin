namespace CryptoBot.Domain.MarketData;

/// <summary>
/// Ước lượng sau 24 giờ giá có thể lên/xuống bao nhiêu, dựa trên biến động của các ngày đã qua.
/// Không dự đoán hướng: chỉ nói "thường dao động trong khoảng nào". Giả định biến động sắp tới giống quá khứ,
/// nên ngày có tin lớn có thể vượt khoảng 95%.
/// </summary>
/// <param name="Days">Số thay đổi theo ngày dùng để tính.</param>
/// <param name="Price">Giá làm mốc.</param>
/// <param name="TypicalMovePercent">Độ lệch chuẩn của % thay đổi một ngày (σ).</param>
/// <param name="Low68">Giá thấp của khoảng ±1σ (khoảng 68% số ngày nằm trong).</param>
/// <param name="High68">Giá cao của khoảng ±1σ.</param>
/// <param name="Low95">Giá thấp của khoảng ±2σ (khoảng 95% số ngày nằm trong).</param>
/// <param name="High95">Giá cao của khoảng ±2σ.</param>
/// <param name="AverageMovePercent">Trung bình độ lớn thay đổi một ngày (không tính dấu).</param>
/// <param name="LargestRisePercent">Ngày tăng mạnh nhất (%).</param>
/// <param name="LargestDropPercent">Ngày giảm mạnh nhất (%, số âm).</param>
/// <param name="UpDaysPercent">Tỷ lệ ngày đóng cửa cao hơn ngày trước (%).</param>
public sealed record DailyMoveRange(
    int Days,
    decimal Price,
    decimal TypicalMovePercent,
    decimal Low68,
    decimal High68,
    decimal Low95,
    decimal High95,
    decimal AverageMovePercent,
    decimal LargestRisePercent,
    decimal LargestDropPercent,
    decimal UpDaysPercent)
{
    /// <summary>Cần ít nhất ngần này thay đổi theo ngày thì số liệu mới có ý nghĩa.</summary>
    public const int MinDays = 10;

    /// <summary>
    /// Tính từ giá đóng cửa theo ngày (tăng dần theo thời gian, chỉ ngày đã đóng).
    /// Trả null khi chưa đủ <see cref="MinDays"/> thay đổi.
    /// </summary>
    public static DailyMoveRange? Compute(IReadOnlyList<decimal> dailyCloses, decimal price)
    {
        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Giá phải dương.");
        if (dailyCloses.Any(c => c <= 0))
            throw new ArgumentException("Giá đóng cửa phải dương.", nameof(dailyCloses));
        if (dailyCloses.Count - 1 < MinDays)
            return null;

        // Log return cộng dồn được qua nhiều ngày và đối xứng lên/xuống, nên dùng để tính σ.
        List<double> logReturns = new(dailyCloses.Count - 1);
        List<decimal> changes = new(dailyCloses.Count - 1);
        for (int i = 1; i < dailyCloses.Count; i++)
        {
            logReturns.Add(Math.Log((double)(dailyCloses[i] / dailyCloses[i - 1])));
            changes.Add((dailyCloses[i] / dailyCloses[i - 1] - 1) * 100);
        }

        double mean = logReturns.Average();
        double sigma = Math.Sqrt(logReturns.Sum(r => (r - mean) * (r - mean)) / (logReturns.Count - 1));

        return new DailyMoveRange(
            Days: changes.Count,
            Price: price,
            TypicalMovePercent: (decimal)((Math.Exp(sigma) - 1) * 100),
            Low68: price * (decimal)Math.Exp(-sigma),
            High68: price * (decimal)Math.Exp(sigma),
            Low95: price * (decimal)Math.Exp(-2 * sigma),
            High95: price * (decimal)Math.Exp(2 * sigma),
            AverageMovePercent: changes.Average(Math.Abs),
            LargestRisePercent: changes.Max(),
            LargestDropPercent: changes.Min(),
            UpDaysPercent: (decimal)changes.Count(c => c > 0) / changes.Count * 100);
    }
}
