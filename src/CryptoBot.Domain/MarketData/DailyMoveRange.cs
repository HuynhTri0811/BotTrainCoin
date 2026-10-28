namespace CryptoBot.Domain.MarketData;

/// <summary>
/// Ước lượng sau <see cref="HorizonDays"/> ngày giá có thể lên/xuống bao nhiêu, dựa trên biến động của các ngày đã qua.
/// Không dự đoán hướng: chỉ nói "thường dao động trong khoảng nào". Giả định biến động sắp tới giống quá khứ
/// và các ngày độc lập với nhau (σ của N ngày = σ một ngày × √N), nên giai đoạn có tin lớn hoặc xu hướng mạnh
/// có thể vượt khoảng 95%.
/// </summary>
/// <param name="HorizonDays">Số ngày phía trước được ước lượng.</param>
/// <param name="Days">Số thay đổi theo ngày dùng để tính σ.</param>
/// <param name="Price">Giá làm mốc.</param>
/// <param name="TypicalMovePercent">Độ lệch chuẩn của % thay đổi sau <see cref="HorizonDays"/> ngày (σ).</param>
/// <param name="Low68">Giá thấp của khoảng ±1σ (khoảng 68% trường hợp nằm trong).</param>
/// <param name="High68">Giá cao của khoảng ±1σ.</param>
/// <param name="Low95">Giá thấp của khoảng ±2σ (khoảng 95% trường hợp nằm trong).</param>
/// <param name="High95">Giá cao của khoảng ±2σ.</param>
/// <param name="AverageMovePercent">Trung bình độ lớn thay đổi sau <see cref="HorizonDays"/> ngày trong lịch sử (không tính dấu).</param>
/// <param name="LargestRisePercent">Lần tăng mạnh nhất sau <see cref="HorizonDays"/> ngày trong lịch sử (%).</param>
/// <param name="LargestDropPercent">Lần giảm mạnh nhất sau <see cref="HorizonDays"/> ngày trong lịch sử (%, số âm).</param>
/// <param name="UpDaysPercent">Tỷ lệ lần giá sau <see cref="HorizonDays"/> ngày cao hơn lúc đầu (%).</param>
/// <param name="Samples">Số đoạn <see cref="HorizonDays"/> ngày (trượt từng ngày) dùng cho các số liệu lịch sử.</param>
public sealed record DailyMoveRange(
    int HorizonDays,
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
    decimal UpDaysPercent,
    int Samples)
{
    /// <summary>Cần ít nhất ngần này thay đổi theo ngày (và ngần này đoạn lịch sử) thì số liệu mới có ý nghĩa.</summary>
    public const int MinDays = 10;

    /// <summary>
    /// Tính từ giá đóng cửa theo ngày (tăng dần theo thời gian, chỉ ngày đã đóng) cho khoảng
    /// <paramref name="horizonDays"/> ngày tới. Trả null khi chưa đủ <see cref="MinDays"/> thay đổi
    /// hoặc chưa đủ <see cref="MinDays"/> đoạn lịch sử dài <paramref name="horizonDays"/> ngày.
    /// </summary>
    public static DailyMoveRange? Compute(IReadOnlyList<decimal> dailyCloses, decimal price, int horizonDays = 1)
    {
        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Giá phải dương.");
        DailyCloses.Validate(dailyCloses, horizonDays);
        if (dailyCloses.Count - 1 < MinDays || dailyCloses.Count - horizonDays < MinDays)
            return null;

        // Log return cộng dồn được qua nhiều ngày và đối xứng lên/xuống, nên dùng để tính σ.
        List<double> logReturns = new(dailyCloses.Count - 1);
        for (int i = 1; i < dailyCloses.Count; i++)
            logReturns.Add(Math.Log((double)(dailyCloses[i] / dailyCloses[i - 1])));

        double mean = logReturns.Average();
        double dailySigma = Math.Sqrt(logReturns.Sum(r => (r - mean) * (r - mean)) / (logReturns.Count - 1));
        double sigma = dailySigma * Math.Sqrt(horizonDays);

        // Thay đổi thực tế sau horizonDays ngày, trượt từng ngày một.
        List<decimal> changes = new(dailyCloses.Count - horizonDays);
        for (int i = horizonDays; i < dailyCloses.Count; i++)
            changes.Add(PercentChange.Between(dailyCloses[i - horizonDays], dailyCloses[i]));

        return new DailyMoveRange(
            HorizonDays: horizonDays,
            Days: logReturns.Count,
            Price: price,
            TypicalMovePercent: (decimal)((Math.Exp(sigma) - 1) * 100),
            Low68: price * (decimal)Math.Exp(-sigma),
            High68: price * (decimal)Math.Exp(sigma),
            Low95: price * (decimal)Math.Exp(-2 * sigma),
            High95: price * (decimal)Math.Exp(2 * sigma),
            AverageMovePercent: changes.Average(Math.Abs),
            LargestRisePercent: changes.Max(),
            LargestDropPercent: changes.Min(),
            UpDaysPercent: PercentChange.UpShare(changes),
            Samples: changes.Count);
    }
}
